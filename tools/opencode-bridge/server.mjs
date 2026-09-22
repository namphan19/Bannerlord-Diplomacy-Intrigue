#!/usr/bin/env node
// MCP server bridging Claude Code to the opencode CLI, for this project.
//
// Role split (per the lead's design): Claude Code is BA/tech lead, opencode is
// dev/tester. This server just shells out to `opencode run` and hands back its
// output as tool results, so Claude Code can delegate a task and read the reply
// without a human relaying text between two terminals.
//
// opencode has no TTY when spawned this way, so permission prompts would hang
// forever with no way to answer them. `--auto` is required for that reason, not
// as a convenience: it makes opencode auto-approve permissions that its own
// config does not explicitly deny. That means an opencode.json permission
// policy in the project root is the actual safety boundary for what a
// delegated task can do (see README.md next to this file) - deny destructive
// git/game-folder commands there if this bridge stays wired up long-term.

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
// tools/opencode-bridge -> Claude's checkout root is two levels up.
const CLAUDE_REPO_DIR = path.resolve(__dirname, "..", "..");

// Claude and opencode work in two separate clones of the same GitHub repo
// (namphan19/Bannerlord-Diplomacy-Intrigue) - not one shared folder. opencode
// must run in its OWN checkout, not Claude's, or it edits/commits into the
// wrong working tree. Set OPENCODE_PROJECT_DIR explicitly (see .mcp.json);
// the sibling "<repo>.opencode" guess below is only a fallback for a fresh
// setup that hasn't set it yet.
const SIBLING_GUESS = path.resolve(CLAUDE_REPO_DIR, "..", path.basename(CLAUDE_REPO_DIR) + ".opencode");
const PROJECT_DIR = process.env.OPENCODE_PROJECT_DIR
  ? path.resolve(process.env.OPENCODE_PROJECT_DIR)
  : fs.existsSync(SIBLING_GUESS)
  ? SIBLING_GUESS
  : CLAUDE_REPO_DIR;

const DEFAULT_TIMEOUT_S = 600;
const MAX_TIMEOUT_S = 1800;

function stripAnsi(s) {
  // eslint-disable-next-line no-control-regex
  return s.replace(/\x1B\[[0-9;]*[a-zA-Z]/g, "");
}

function runOpencode(args, { timeoutSeconds }) {
  return new Promise((resolve) => {
    const child = spawn("opencode", args, {
      cwd: PROJECT_DIR,
      shell: process.platform === "win32",
      windowsHide: true,
    });

    let stdout = "";
    let stderr = "";
    let timedOut = false;

    const timer = setTimeout(() => {
      timedOut = true;
      child.kill();
    }, timeoutSeconds * 1000);

    child.stdout.on("data", (d) => (stdout += d.toString()));
    child.stderr.on("data", (d) => (stderr += d.toString()));

    // Resolve on "exit", not "close": on Windows, spawning a .cmd shim through
    // shell:true means "close" waits for every stdio handle in the whole
    // process (sub)tree to shut, including any MCP-server child opencode
    // itself spawned (e.g. gabs.exe) that can outlive the run and keep a pipe
    // open. That left every call here hanging well past a finished reply -
    // confirmed 2026-09-22, opencode had already replied but "close" never
    // fired even at 180s. "exit" fires once this immediate child is done,
    // which is what we actually want to wait for.
    let settled = false;
    const settle = (code) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve({ code, stdout, stderr: stripAnsi(stderr), timedOut });
    };
    child.on("exit", settle);
    child.on("close", settle);

    child.on("error", (err) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve({ code: -1, stdout, stderr: String(err), timedOut: false });
    });
  });
}

// opencode --format json prints one JSON object per line (NDJSON). A part can
// be re-emitted as it streams, so keep only the latest text per part id and
// join in first-seen order; that gives the final message text without dupes.
function parseNdjson(stdout) {
  const textByPart = new Map();
  const partOrder = [];
  const toolCalls = [];
  let sessionId = null;
  let totalCost = 0;
  let tokens = { input: 0, output: 0, reasoning: 0 };

  for (const line of stdout.split("\n")) {
    const trimmed = line.trim();
    if (!trimmed) continue;
    let evt;
    try {
      evt = JSON.parse(trimmed);
    } catch {
      continue; // non-JSON noise on stdout, ignore
    }

    sessionId = evt.sessionID ?? sessionId;
    const part = evt.part;
    if (!part) continue;

    if (part.type === "text" && typeof part.text === "string") {
      if (!textByPart.has(part.id)) partOrder.push(part.id);
      textByPart.set(part.id, part.text);
    } else if (part.type === "tool" || part.type === "tool-invocation" || part.type === "tool_use") {
      const name = part.tool ?? part.name ?? "unknown-tool";
      const status = part.state?.status ?? part.status ?? "unknown";
      toolCalls.push(`${name} (${status})`);
    } else if (part.type === "step-finish") {
      totalCost += part.cost ?? 0;
      if (part.tokens) {
        tokens.input += part.tokens.input ?? 0;
        tokens.output += part.tokens.output ?? 0;
        tokens.reasoning += part.tokens.reasoning ?? 0;
      }
    }
  }

  const text = partOrder.map((id) => textByPart.get(id)).join("\n").trim();
  return { text, sessionId, toolCalls, totalCost, tokens };
}

const server = new McpServer({
  name: "opencode-bridge",
  version: "1.0.0",
});

server.registerTool(
  "opencode_delegate",
  {
    title: "Delegate a task to opencode (dev/tester)",
    description:
      "Hand a task to the opencode CLI running in this project's directory and return its final reply. " +
      "Pass session_id to continue a prior conversation (e.g. to answer opencode's follow-up questions or " +
      "request fixes on the same piece of work); omit it to start a new one. Runs with --auto, so opencode " +
      "auto-approves any permission its own opencode.json config does not explicitly deny - review that " +
      "config before delegating anything destructive.",
    inputSchema: {
      task: z.string().describe("The task/instruction to send to opencode, written as you would to a developer."),
      session_id: z.string().optional().describe("An opencode session id to continue, from a previous opencode_delegate call."),
      agent: z.string().optional().describe("opencode agent to use (see opencode_list_sessions' sibling `opencode agent list` if you need to check names). Omit to use opencode's default."),
      title: z.string().optional().describe("Title for a new session (ignored when continuing one)."),
      timeout_seconds: z.number().int().positive().max(MAX_TIMEOUT_S).optional().describe(`Max time to wait (default ${DEFAULT_TIMEOUT_S}, max ${MAX_TIMEOUT_S}).`),
    },
  },
  async ({ task, session_id, agent, title, timeout_seconds }) => {
    const args = ["run", "--format", "json", "--auto"];
    if (session_id) args.push("--session", session_id);
    if (agent) args.push("--agent", agent);
    if (title && !session_id) args.push("--title", title);
    args.push(task);

    const timeoutSeconds = timeout_seconds ?? DEFAULT_TIMEOUT_S;
    const { code, stdout, stderr, timedOut } = await runOpencode(args, { timeoutSeconds });
    const { text, sessionId, toolCalls, totalCost, tokens } = parseNdjson(stdout);

    if (timedOut) {
      return {
        isError: true,
        content: [
          {
            type: "text",
            text: `opencode timed out after ${timeoutSeconds}s and was killed. Partial session: ${sessionId ?? "(none)"}.\n\nPartial reply so far:\n${text || "(nothing)"}`,
          },
        ],
      };
    }

    if (code !== 0 && !text) {
      return {
        isError: true,
        content: [
          { type: "text", text: `opencode exited with code ${code}.\n\nstderr:\n${stderr || "(empty)"}` },
        ],
      };
    }

    const summary = [
      `session_id: ${sessionId ?? "(unknown)"}`,
      toolCalls.length ? `tool calls: ${toolCalls.join(", ")}` : null,
      `cost: $${totalCost.toFixed(4)} | tokens in/out/reasoning: ${tokens.input}/${tokens.output}/${tokens.reasoning}`,
      "",
      text || "(opencode returned no text reply - check tool calls above or run opencode_list_sessions)",
    ]
      .filter((l) => l !== null)
      .join("\n");

    return { content: [{ type: "text", text: summary }] };
  }
);

server.registerTool(
  "opencode_list_sessions",
  {
    title: "List opencode sessions",
    description: "List opencode's local sessions for this project (raw `opencode session list` output).",
    inputSchema: {},
  },
  async () => {
    const { code, stdout, stderr } = await runOpencode(["session", "list"], { timeoutSeconds: 30 });
    if (code !== 0) {
      return { isError: true, content: [{ type: "text", text: stderr || `exit code ${code}` }] };
    }
    return { content: [{ type: "text", text: stripAnsi(stdout) || "(no sessions)" }] };
  }
);

server.registerTool(
  "opencode_delete_session",
  {
    title: "Delete an opencode session",
    description: "Delete a local opencode session record by id (does not touch project files).",
    inputSchema: {
      session_id: z.string().describe("The session id to delete, as shown by opencode_list_sessions."),
    },
  },
  async ({ session_id }) => {
    const { code, stdout, stderr } = await runOpencode(["session", "delete", session_id], { timeoutSeconds: 30 });
    if (code !== 0) {
      return { isError: true, content: [{ type: "text", text: stderr || `exit code ${code}` }] };
    }
    return { content: [{ type: "text", text: stripAnsi(stdout) || `Deleted ${session_id}.` }] };
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);

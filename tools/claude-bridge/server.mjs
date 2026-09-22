#!/usr/bin/env node
// MCP server bridging opencode to Claude Code, for this project.
//
// This is the reverse of tools/opencode-bridge: it is registered on opencode's
// side (its project opencode.jsonc), not Claude's, so opencode can ask Claude
// Code (BA/tech lead) a question mid-task instead of only ever being the one
// that gets asked.
//
// Deliberately read-only and advisory. It spawns `claude -p` restricted to
// Read/Grep/Glob (--allowedTools) and never passes a permission-bypass flag.
// Two reasons, not one:
//   1. Two Claude processes with write access to the same working tree at the
//      same time is how you corrupt a working tree - the interactive Claude
//      session may be mid-edit when opencode reaches out.
//   2. This tool's job is "answer a question", not "take an action". If the
//      answer implies a code change, that change should go back through the
//      normal opencode_delegate path (or the human), not happen silently
//      inside a question-answering call.
// If that scope ever needs to widen, widen it deliberately - don't add
// --permission-mode bypassPermissions here as a quick fix for a prompt that
// hung; that reintroduces both problems above.

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
// This file lives in the shared repo, so it exists in both checkouts after a
// git sync. When opencode runs it, __dirname is somewhere under opencode's
// OWN checkout (e.g. ".../bannerlord.mod.opencode/tools/claude-bridge") - the
// sibling without the ".opencode" suffix is Claude's checkout.
const OPENCODE_REPO_DIR = path.resolve(__dirname, "..", "..");
const SIBLING_GUESS = OPENCODE_REPO_DIR.endsWith(".opencode")
  ? OPENCODE_REPO_DIR.slice(0, -".opencode".length)
  : OPENCODE_REPO_DIR;
const CLAUDE_PROJECT_DIR = process.env.CLAUDE_PROJECT_DIR
  ? path.resolve(process.env.CLAUDE_PROJECT_DIR)
  : fs.existsSync(SIBLING_GUESS)
  ? SIBLING_GUESS
  : OPENCODE_REPO_DIR;

const DEFAULT_TIMEOUT_S = 300;
const MAX_TIMEOUT_S = 900;
const READ_ONLY_TOOLS = "Read Grep Glob";

function runClaude(args, { timeoutSeconds }) {
  return new Promise((resolve) => {
    const child = spawn("claude", args, {
      cwd: CLAUDE_PROJECT_DIR,
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

    // Resolve on "exit", not "close" - see the matching comment in
    // tools/opencode-bridge/server.mjs. Same Windows shell:true + .cmd shim
    // issue applies here.
    let settled = false;
    const settle = (code) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve({ code, stdout, stderr, timedOut });
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

// `claude -p --output-format json` prints one JSON object for the whole run
// (not NDJSON). Parse defensively since the exact field set isn't pinned to a
// verified version here - this was written against documented flags, not a
// live sample, because spawning a nested `claude -p` from inside this session
// is blocked by this harness's own safety classifier.
function parseClaudeResult(stdout) {
  const trimmed = stdout.trim();
  try {
    const evt = JSON.parse(trimmed);
    return {
      text: evt.result ?? evt.text ?? trimmed,
      sessionId: evt.session_id ?? evt.sessionId ?? null,
      isError: Boolean(evt.is_error),
      costUsd: evt.total_cost_usd ?? evt.cost_usd ?? null,
    };
  } catch {
    return { text: trimmed, sessionId: null, isError: false, costUsd: null };
  }
}

const server = new McpServer({
  name: "claude-bridge",
  version: "1.0.0",
});

server.registerTool(
  "ask_claude",
  {
    title: "Ask Claude Code (BA/tech lead) a question",
    description:
      "Ask Claude Code a question about this project - design intent, an ambiguous requirement, whether an " +
      "approach fits the architecture, etc. Read-only: this runs Claude Code restricted to Read/Grep/Glob, " +
      "it cannot edit files or run commands. Pass session_id to continue a prior question thread.",
    inputSchema: {
      question: z.string().describe("The question to ask Claude Code, with enough context to answer without seeing your session."),
      session_id: z.string().optional().describe("A Claude session id to continue, from a previous ask_claude call's reply."),
      timeout_seconds: z.number().int().positive().max(MAX_TIMEOUT_S).optional().describe(`Max time to wait (default ${DEFAULT_TIMEOUT_S}, max ${MAX_TIMEOUT_S}).`),
    },
  },
  async ({ question, session_id, timeout_seconds }) => {
    const args = ["-p", "--output-format", "json", "--allowedTools", READ_ONLY_TOOLS];
    if (session_id) args.push("--resume", session_id);
    args.push(question);

    const timeoutSeconds = timeout_seconds ?? DEFAULT_TIMEOUT_S;
    const { code, stdout, stderr, timedOut } = await runClaude(args, { timeoutSeconds });

    if (timedOut) {
      return {
        isError: true,
        content: [{ type: "text", text: `claude timed out after ${timeoutSeconds}s and was killed.` }],
      };
    }
    if (code !== 0) {
      return {
        isError: true,
        content: [{ type: "text", text: `claude exited with code ${code}.\n\nstderr:\n${stderr || "(empty)"}` }],
      };
    }

    const { text, sessionId, isError, costUsd } = parseClaudeResult(stdout);
    const summary = [
      `session_id: ${sessionId ?? "(unknown)"}`,
      costUsd != null ? `cost: $${costUsd.toFixed(4)}` : null,
      "",
      text || "(claude returned no text)",
    ]
      .filter((l) => l !== null)
      .join("\n");

    return { isError: Boolean(isError), content: [{ type: "text", text: summary }] };
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);

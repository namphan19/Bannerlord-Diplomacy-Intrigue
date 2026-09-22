# opencode-bridge

An MCP server that lets Claude Code hand tasks to the `opencode` CLI and read
back its reply, so Claude Code (BA / tech lead) can delegate implementation
and testing work to opencode (dev / tester) without a human relaying text
between two terminals.

It does not give opencode any capability it didn't already have from its own
CLI — it just runs `opencode run --format json --auto ...` as a subprocess,
parses the NDJSON event stream, and returns the final text reply plus the
session id, tool calls made, and cost/tokens.

**Two separate checkouts, one channel each way for now.** Claude Code and
opencode work in two independent clones of the same GitHub repo
(`namphan19/Bannerlord-Diplomacy-Intrigue`) — Claude's is `bannerlord.mod`,
opencode's is the sibling `bannerlord.mod.opencode` (currently on
`feature/ui-proposal`), not the same folder. This bridge always runs opencode
in **its own** checkout via `OPENCODE_PROJECT_DIR` (set in `.mcp.json`), never
Claude's — running it in the wrong tree would mean it edits and commits into
the wrong branch's working copy.

That means today there are two channels, not a symmetric one:
- **Claude → opencode**: this bridge, synchronous. Claude calls
  `opencode_delegate`, opencode replies in the same call.
- **opencode → Claude, at the code level**: git. opencode commits and pushes
  branches in its own clone against the shared origin; Claude fetches/reviews
  them there. That's the durable channel for what opencode actually built,
  independent of this bridge.

The reverse *tool* channel (opencode paging Claude mid-task with a question)
is `tools/claude-bridge`, registered on opencode's side. Unlike this bridge it
is read-only/advisory on purpose — see its README for why.

## Setup

```bash
cd tools/opencode-bridge
npm install
```

Then register it with Claude Code by adding this to `.mcp.json` in the
project root (create the file if it doesn't exist), pointing
`OPENCODE_PROJECT_DIR` at opencode's own checkout:

```json
{
  "mcpServers": {
    "opencode-bridge": {
      "command": "node",
      "args": ["tools/opencode-bridge/server.mjs"],
      "env": {
        "OPENCODE_PROJECT_DIR": "C:\\Users\\nambi\\Desktop\\bannerlord.mod.opencode"
      }
    }
  }
}
```

Claude Code will ask to approve the new project MCP server the next time it
starts in this repo (or restart it now for the prompt to appear).

## Tools exposed

- **opencode_delegate** — send a task; optionally continue a prior session
  with `session_id` (e.g. to answer opencode's follow-up questions or ask for
  a fix on the same piece of work).
- **opencode_list_sessions** — list opencode's local sessions for this
  project.
- **opencode_delete_session** — delete a session record (does not touch
  project files).

## Known latency: opencode's own startup, not this bridge

Verified 2026-09-22: every `opencode run` invocation does a synchronous
`cleanup prune=7.days` pass over its local `opencode.db` (SQLite, event-log
style — one project can be 100MB+) before the session is created or anything
starts streaming. On this machine that took 44s → 89s → 180s across
successive test calls (measured from `~/.local/share/opencode/log/opencode.log`,
`init` → `cleanup prune` → `created id=...`). Root cause: the `event` table
storing every internal step/tool-call/snapshot, ~9.7KB/row, accumulated from
real session history — confirmed via `dbstat`, not fragmentation
(`freelist_count` was 0).

This is opencode's own behavior, not something `runOpencode()` here causes or
can skip via a flag. Two implications for using this bridge:
- Budget for it: don't set `timeout_seconds` below ~120-180s expecting a fast
  reply, even for a trivial task. The default (600s) already has headroom.
- If it keeps growing, the fix is pruning opencode's own session history
  (`opencode_delete_session` / `opencode session delete <id>`), not anything
  here. The lead chose to leave it as-is for now (2026-09-22).

## The `--auto` tradeoff — read this before relying on it

`opencode run` has no TTY when spawned this way, so a permission prompt would
hang forever with nothing able to answer it. The bridge therefore always
passes `--auto`, which makes opencode auto-approve any permission its own
config does not explicitly deny.

That makes opencode's **own** permission policy the actual safety boundary,
not this bridge. Two ways to set it (see opencode's docs for the current
field names, since this wasn't verified against a live config in this repo):

- A `permission` block in `opencode.json` in the project root, denying
  patterns like `git push --force*`, `rm -rf*`, or anything touching
  `deploy.ps1` / the game install directory.
- A restricted `--agent` passed to `opencode_delegate`, if you define a
  dev/tester agent with `opencode agent create` that only gets the tools it
  actually needs (edit + bash-in-repo, not arbitrary shell).

Until one of those is in place, treat every `opencode_delegate` call as
"opencode can run any shell command in this repo, unsupervised."

## Workflow this was built for

- **Product owner (user)**: sets direction, makes design calls.
- **Claude Code**: BA / tech lead — breaks work down, calls
  `opencode_delegate` to hand a task to opencode, reviews what comes back.
- **opencode**: dev / tester — implements, runs tests, reports back through
  the same tool call.

Claude Code should still apply its own judgment about what to delegate vs. do
itself, and should surface opencode's output to the user rather than treating
it as ground truth without review.

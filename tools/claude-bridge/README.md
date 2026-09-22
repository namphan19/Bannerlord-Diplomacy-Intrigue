# claude-bridge

The reverse of `tools/opencode-bridge`: an MCP server registered on **opencode's**
side (its `opencode.jsonc`) that lets opencode ask Claude Code a question
mid-task — design intent, an ambiguous requirement, whether an approach fits
the architecture — instead of only ever being the one that gets asked.

## Read-only by design

This does not mirror `opencode_delegate`'s `--auto`. It runs
`claude -p --allowedTools "Read Grep Glob" ...` with no permission-bypass
flag, so the headless Claude instance it spawns can read and reason but
cannot edit files or run commands. Two reasons:

1. Two Claude processes with write access to the same working tree at once is
   how you corrupt a working tree — the interactive Claude session in
   `bannerlord.mod` may be mid-edit when opencode reaches out.
2. This tool's job is to answer a question, not take an action. If the answer
   implies a code change, that change should go back through the normal
   `opencode_delegate` path (or the human), not happen silently inside a
   question-answering call.

If that scope ever needs to widen, widen it deliberately — don't reach for
`--permission-mode bypassPermissions` as a quick fix for a prompt that hung;
that reintroduces both problems above.

**Not live-verified end to end.** The exact JSON shape `claude -p
--output-format json` prints was written against the CLI's documented flags,
not a captured sample — spawning a nested `claude -p` from inside a Claude
Code session is blocked by that session's own safety classifier
("Create Unsafe Agents"), so this couldn't be smoke-tested the way
`opencode-bridge` was. What is verified: `opencode mcp list` shows
`claude-bridge` as `connected`, meaning the server starts and completes the
MCP handshake correctly. The first real `ask_claude` call is the first live
test of the `claude -p` invocation itself — check its output against what you
expect before trusting the pattern.

## Setup

Lives in the shared repo, so it exists in both checkouts once committed and
pulled. Per checkout:

```bash
cd tools/claude-bridge
npm install
```

Registered in opencode's project config, `opencode.jsonc` at the root of
opencode's checkout (`bannerlord.mod.opencode`, not this one):

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "claude-bridge": {
      "type": "local",
      "command": ["node", "tools/claude-bridge/server.mjs"],
      "environment": {
        "CLAUDE_PROJECT_DIR": "C:\\Users\\nambi\\Desktop\\bannerlord.mod"
      },
      "enabled": true
    }
  }
}
```

`CLAUDE_PROJECT_DIR` must point at Claude's checkout, not opencode's own —
this server runs a headless `claude -p` there so it has the real CLAUDE.md,
docs, and source to answer from.

## Tool exposed

- **ask_claude(question, session_id?, timeout_seconds?)** — pass `session_id`
  from a prior reply to continue the same question thread.

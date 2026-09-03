# The Self-Improving Loop — 22 PDSA cycles on this project

**English · [한국어](README-ko.md)**

This project did not just *build* a PDSA tool. It was **built by one** — 22 cycles since 2026-08-26 ran
through the `pdsa` CLI's own Plan → Do → Study → Act loop, and every step is still in the graph.
(Not every commit rode a cycle; which ones did not, and why, is its own chapter below.)

The Plan/Do/Study/Act text below is the **actual recorded content**, condensed for reading and
translated from the Korean original. The raw record is always available from the loop's own memory:

```bash
pdsa history --to 22 --full --project akka-graph-loop   # the whole timeline, oldest first
pdsa show 21 --full --project akka-graph-loop           # one cycle's four phases, verbatim
```

> Those two commands did not exist when this page was started. Writing it meant hand-parsing the
> graph JSON, which is exactly the kind of gap the loop is supposed to surface — so it became
> cycle #22. This document is the first artifact produced *entirely* through the CLI it describes.

---

## What 22 cycles actually changed

| | Cycle 1 | Cycle 22 |
|---|---|---|
| Tests | — | **271** |
| Could the loop judge itself? | no `expected` field existed | `met / partial / unmet` per cycle |
| LLM providers | 1 (OpenAI API key) | **5** (API key · keyless local · GPT OAuth · Codex · `claude -p`) |
| Agent-readable output | prose only | `--json` on 9 commands |
| Does a failed LLM call corrupt memory? | **yes — 100% of the time** | **no — 0/20 injected failures** |
| Can you query a past cycle? | no | `pdsa show <n>` / `pdsa history` |

**Expectation hit rate: 11 / 18 judged cycles (61%).** That number understates the trend:

```
cycles 4–14 (first 10 judged)    ████░░░░░░   4 met  →  40%
cycles 15–22 (last 8 judged)     ████████░░   7 met  →  88%
```

The loop got better at *predicting itself*, which is the actual point of writing a verifiable
expected outcome before doing the work.

### The shape of the record

Three cycles produced **no verdict**, and that is history rather than missing data. **#1–#3** ran
before the closed loop existed; **#11** is the only cycle ever abandoned mid-flight (planned, never
studied). The loop does not quietly delete its own loose ends. **#12 is the only `unmet`** — the plan
asked for research documents, the work delivered code, and Study refused to round that up to success.

Five cycles are **reinforcement cycles** (`REINFORCES` edges):

```
#4 ← #5      #12 ← #13      #14 ← #15 ← #16      #20 ← #21
```

Reading the record straight surfaces one more thing. **Ten cycles recorded `reinforce: yes` in Act, but
only five edges exist.** The five that never linked are #5–#9, a contiguous run in Era 3. Auto-linking
(`PendingReinforceTarget`) already existed by then, so the likely explanation is a deliberate `--fresh`,
but **whether `--fresh` was used is not recorded, so it cannot be reconstructed today.** That is an
observability gap the loop found in its own record, still open.

---

# The story

What follows is a **development story**, not a list of cycle summaries. Read the PDSA record and the
git log side by side and you can see which commit each cycle landed in — and which commits shipped
with no cycle at all. The raw record (expected, verdict, actual, reinforce) sits in a collapsible
block at the end of each chapter.

> Clock note: PDSA records timestamps in UTC, git logs them in KST (+9). Everything below is KST.

**What is inside the collapsible blocks** — the loop records different attributes at each step. The
story folds them in; the raw values sit at the end of each chapter.

| Phase | Recorded attributes | Cycles with a value |
|---|---|---|
| **Plan** | the plan text + **expected** (`expected`) — the criterion Study will judge against | 22 / 19 |
| **Do** | the execution report | 21 |
| **Study** | **verdict** + **actual** — the measurement that pairs with the expectation | 18 |
| **Act** | the next improvement action + **reinforce** (`yes:what` or `no`) | 18 |
| all phases | **metrics** (latency · attempts · model · tokens) — built by #21, so only after it | 2 |

`Expected → Actual → Verdict` forms one triangle. The per-phase LLM coaching prose (hypothesis,
Plan→Do summary, learnings) runs past 1,000 characters per cycle and is not reproduced here —
`pdsa show <n> --full` has the originals.

---

## Chapter 0 · Twelve commits made by hand

At 00:50 on 26 August 2026, the first commit had nothing to do with PDSA. `fc51b4a` — a study project
for Akka.NET Streams' Graph DSL. Fan-in and fan-out, partial graphs, and the trickiest part, the one
this repository is named after: cycles.

Four hours later that study turns. `abe24aa` adds a sample implementing Deming's PDSA loop **as a real
Akka.Streams feedback cycle**, and `14413eb` starts recording those rounds into an embedded Kùzu graph
DB in real time. `aeb8a68` builds a viewer for that graph, and `338aad3` finally creates the `pdsa`
CLI itself. `5f037a0` gives it plan/do/study/act; `3126030` ships a Claude Code skill.

By 09:37, all twelve commits had been made **by hand**. The tool was finished, and had never been
pointed at itself.

Then at 13:07 someone typed `pdsa plan "verify the pdsa-cli basics and strengthen the unit tests"`.
**Cycle #1.** The moment the tool turned on its author.

---

## Chapter 1 · The tool turns on itself — and cannot judge what it finds

### #1 — Verify the CLI basics and grow the unit tests

![Cycle 1](cards/cycle-01.svg)

The first cycle started dull and found something that stung. Running through the commands
(`version`, `project`, `check`, `status`) and confirming graph writes and reads turned up the fact
that **the test project did not reference `pdsa-cli` at all**. There were tests; not one line of CLI
code ran through them.

And that discovery could not be called a success, because there was nothing to compare it against.
`pdsa` had no `expected` field at this point — Plan took down the plan sentence and never asked what
would count as success. Four phases faithfully recorded, verdict column empty, cycle closed.

### #2 — Cover the pure Cli/Llm logic with unit tests

![Cycle 2](cards/cycle-02.svg)

Doing what #1's Act said: a `PdsaCli` reference and `InternalsVisibleTo` into the test csproj, then
five files across ArgUtil, CommandRouter, PdsaCoach, OpenAiConfig and PdsaProjectPaths.

It looks like the boring cycle and it paid the longest interest. The pure-logic tests laid down here
became the **regression gate** through Chapter 3 (auth, rebuilt three times) and Chapter 4 (i18n).
Almost every later cycle's expected outcome contains "all N existing tests pass" — this is where that
N starts.

### #3 — Graph viewer: show the active project, switch without a restart

![Cycle 3](cards/cycle-03.svg)

`/api/projects` and `/api/graph?project=` on the viewer, project name and DB path in the header, a
dropdown that switches without restarting. At 13:57 the output of #1, #2 and #3 goes out together as
`d3e48e7`.

And once the accumulated graph could actually be **looked at**, what was missing became obvious. The
screen held cycle and phase nodes and nothing else. No expectations, no verdicts, no relationships
between cycles. The viewer was less a feature than the instrument that made the next improvement
visible.

<details>
<summary>The raw record for this chapter</summary>

- **#1** no expected · no verdict · no reinforce · → `d3e48e7`
- **#2** no expected · no verdict · no reinforce · → `d3e48e7`
- **#3** no expected · no verdict · no reinforce · → `d3e48e7`

All three predate the `expected`/`verdict` fields entirely — the blanks are the schema of that moment,
not missing data.
</details>

---

## Chapter 2 · The loop closes — and hands itself a `partial`

### #4 — Close the loop: expected → verdict → REINFORCES

![Cycle 4](cards/cycle-04.svg)

The cycle that started at 14:26 is this project's hinge. Four columns —
`expected`/`verdict`/`actual`/`reinforce` — go into `PdsaWorkflow` via an ALTER migration,
`REINFORCES` edges appear between cycles, and a hit rate starts being computed. `PdsaCoach` learns to
parse sentinel lines (`Expected:`, `Verdict:`) out of the LLM response, and the viewer gets verdict
colors and a hit-rate badge. 14:36, `8b43943`.

The loop can now judge itself. Its first subject was itself.

The result was **`partial`**. All 73 tests passed and the `partial → reinforce → met` flow really did
reproduce, but the eval hit rate sat at 1/2 (50%) and the second half of the expected outcome —
"90% agreement across 10 representative cycles" — was never attempted. A feature working and a verdict
being trustworthy are different claims, the loop wrote about itself.

That honesty became the baseline. Seven of the eighteen judged cycles that follow got a non-clean
verdict. Not one of them passed on "it ran, so it worked".

<details>
<summary>The raw record for this chapter</summary>

- **#4** expected: expected outcome, verdict, reinforcement history and hit rate all connected and
  displayed in CLI and graph view, with verdicts across 10 representative cycles agreeing 90%+ ·
  verdict **`partial`** · actual: 73 tests passing and the flow confirmed, but eval hit rate 1/2 (50%)
  and the 10-sample check unfinished · reinforce `yes:` structure the Study input and judging criteria
  to raise reproducibility · → `8b43943`
</details>

---

## Chapter 3 · Four cycles, one commit — opening up auth

The densest stretch in the project. Between 01:09 and 01:44 on 08-27 — **35 minutes** — four cycles
run, and all of it lands at 01:55 in a single commit: `103a0c4`.

### #5 — Design first: extend LLM auth beyond a single API key

![Cycle 5](cards/cycle-05.svg)

The instruction was explicit: **"plan before you adopt."** So this cycle writes no code. It greps every
call site in the auth/config layer, counts five (`LlmOptions`, the `OpenAiClient` constructor,
`OpenAiConfig`, `ConfigCommand`, and the four places a client gets built), and makes one decision.

**Preserve the `new OpenAiClient(LlmOptions)` signature.**

That single decision governs the rest of the story. The auth expansion gets confined to four files and
the four construction sites never change again. Three more providers — OAuth, Codex, `claude -p` —
attach over the following days and the boundary holds.

The verdict was `partial`: the design stood, but the concrete keyless/OAuth config examples and the
explicit acceptance criteria were missing. Act added four runnable JSON examples and five measurable
criteria.

### #6 — Auth abstraction: AuthMode + IAuthProvider

![Cycle 6](cards/cycle-06.svg)

`AuthMode(ApiKey|OAuth|None)` and `IAuthProvider` land as designed: `ApiKeyAuth`, `NoAuth`, and a still
stubbed `OAuthAuth`. `OpenAiClient` stops fixing its header once in the constructor and calls
`GetHeaderAsync` per request.

One interesting judgement comes out here. Dropping the key requirement while letting any endpoint be
contacted unauthenticated would open a hole. So `IsPrivateEndpoint` auto-allows only loopback, RFC1918,
fc00:: and `.local` — and **treats DNS names as remote**. Reaching a remote endpoint without a key
requires saying so explicitly.

73→91 tests passed, and it was still `partial`: the planned merge tests did not make it in and OAuth
was still a stub.

### #7 — Keyless open-weight E2E and config-merge tests

![Cycle 7](cards/cycle-07.svg)

This cycle had an accident.

While adding a path-injection seam to `OpenAiConfig` and running an E2E against a real keyless server
(`a1.webnori.com`), it turned out .NET's `GetFolderPath` ignores the `LOCALAPPDATA` environment
variable. The test wrote to the real path instead of an isolated temp one and **overwrote the user's
actual global config.** It was reverted, and the seam was put in first to stop it recurring.

Study did not file this as "write tests more carefully". It recorded a structural rule:
**a path-injection seam must precede integration work on anything touching static OS paths.**
100 tests and zero warnings, but the four fields named in the expected outcome were not the four
fields actually verified — `partial` again.

### #8 — GPT OAuth: refresh, persist, device-code polling

![Cycle 8](cards/cycle-08.svg)

The end of Chapter 3 and the project's **first `met`**.

`OAuthToken`, `ITokenRefresher`, `HttpTokenRefresher` (HTTP transport injected), a device-code polling
state machine (delay function and `now` injected). `OAuthAuth` goes from stub to real: it checks expiry
with a 30-second skew, refreshes, injects the Bearer, and persists.

What matters is **what was made injectable**. With the refresher, the transport and the clock all
supplied from outside, every "you have to wait to find out" state — expiry, refresh, `slow_down`
backoff, denial, deadline overrun — became a deterministic unit test. 102→129 tests, zero warnings,
verdict `met`.

Three `partial`s closed here. Eleven minutes later, four cycles ship as one commit.

<details>
<summary>The raw record for this chapter</summary>

- **#5** expected: the design doc specifies the config schema, auth abstraction boundary and
  backward-compat rules, and includes keyless/OAuth config examples and acceptance criteria ·
  verdict **`partial`** · actual: design, five backward-compat regressions and adoption order settled,
  5/5 change points identified, but config examples and acceptance criteria unconfirmed ·
  reinforce `yes:` settle and approve the three open design questions · → `103a0c4`
- **#6** expected: 73 existing plus new auth tests pass, ApiKey keeps its behavior, `provider local`
  loads keyless, remote no-key stays blocked · verdict **`partial`** · actual: 91 passing, keyless load
  and remote blocking confirmed, merge tests not added, OAuth still a stub · reinforce `yes:` introduce
  config path injection and verify `repo<global<env` · → `103a0c4`
- **#7** expected: 91 passing, merge tests verify precedence per field for four fields, E2E returns OK
  with opt-in and blocks without it · verdict **`partial`** · actual: 100 tests and zero warnings,
  blocking (exit 3) and keyless check success (1,270ms) confirmed, named fields differ from verified
  fields · reinforce `yes:` pin the global-only policy for `allow_insecure_no_auth` · → `103a0c4`
- **#8** expected: 102 existing plus OAuth core, config and device-code tests pass, zero warnings,
  expiry/refresh/persist/transitions reproduced · verdict **`met`** · actual: 129 passing, zero
  warnings, refresh, persist, no effect on other modes, `pending`/`slow_down`/success transitions and
  `refresh_token` non-exposure all verified · reinforce `yes:` atomic writes and owner-only permissions
  for `refresh_token_file` · → `103a0c4`
</details>

---

## Chapter 4 · Shipping and localizing — a build can succeed and lose your resources

### #9 — pdsa init: embed the skill inside the binary

![Cycle 9](cards/cycle-09.svg)

`pdsa init` installs the PDSA skill (`.claude/skills/pdsa/SKILL.md`) into the current workspace. Being
an AOT single file, the skill documents had to travel as embedded resources.

Here a non-obvious trap appeared. MSBuild's `AssignCulture` read the `.en`/`.ko` in `SKILL.en.md` and
`SKILL.ko.md` as **culture codes** and split both into satellite assemblies. They collided under the
same name, `PdsaCli.Resources.SKILL.md`, and the result was **zero resources in the main dll**.
The build succeeded.

`WithCulture=false` plus an explicit `LogicalName` turned culture inference off and fixed it. The
lesson: a `--getItem` listing does not prove the resource is embedded. The only decisive checks were
`GetManifestResourceNames` and an actual load test.

The verdict was `partial` — 141 tests and a real CLI E2E passed, but running `init` from the AOT native
exe could not be verified because of a local link-environment problem. 06:40, `ff3b4e9`.

### #10 — i18n across help, coaching and config

![Cycle 10](cards/cycle-10.svg)

The cycle that establishes five levels of precedence: `--lang > PDSA_LANG > config > OS locale > en`.

The obstacle was that `InvariantGlobalization=true`, kept on for AOT, rules out `CultureInfo`. The way
around it was environment variables (`LANG`/`LC_ALL`/`LC_MESSAGES`/`LANGUAGE`) plus a Windows
`GetUserDefaultUILanguage` P/Invoke — swapped from `LibraryImport` to `DllImport` because the former
demanded unsafe.

More important was **isolating the decision into one side-effect-free `PdsaLang.Resolve`**. That is
what let all five precedence levels be pinned by isolated unit tests. 141→163 tests, verdict `met`.
07:22, `7d04df5`.

<details>
<summary>The raw record for this chapter</summary>

- **#9** expected: `pdsa init --lang en|ko --yes` produces a valid `SKILL.md` from the AOT output, all
  existing 102+ and new resource/path/overwrite tests pass, zero build warnings · verdict **`partial`** ·
  actual: 141 tests, 12 new, real CLI E2E and zero warnings achieved; native-exe verification unfinished
  due to the link environment · reinforce `yes:` run `pdsa init` from the native exe in a working
  environment or CI · → `ff3b4e9`
- **#10** expected: 141 existing plus new i18n precedence, Korean detection, prompt-language and help
  tests pass with zero warnings, and the five-level order reproduces per isolated scenario ·
  verdict **`met`** · actual: 163 passing, zero warnings, precedence and ko/en branching confirmed in
  tests and the real CLI · reinforce `no` · → `7d04df5`
</details>

---

## Chapter 5 · Let the agent use its own model — the one failure and the one abandonment

This chapter holds the project's only **abandoned cycle** and its only **`unmet`**.

### #11 — Codex OAuth mode, reusing `~/.codex/auth.json`

![Cycle 11](cards/cycle-11.svg)

At 07:37 a Plan is recorded. Let Codex subscribers reuse the official `codex login` artifacts with
`pdsa` — and the mechanism is written out in detail: the token source, the refresh endpoint, how to
pull `account_id` out of the JWT, the `/responses` SSE.

Then there is no Do. No Study, no Act.

The only cycle of 22 cut off mid-flight. Its `status` still reads **`planned`** rather than `acted`.
The loop does not quietly delete its own loose ends.

**The code shipped anyway.** `a2a1aaf` at 07:47 — "Codex (ChatGPT subscription) OAuth mode
[experimental]" — is precisely this plan implemented. The cycle was abandoned; the work lived and
became a commit. That is the kind of fact only visible with the record and the repository side by side.

### #12 — Research official self-model provider paths

![Cycle 12](cards/cycle-12.svg)

Six minutes later, a clear Plan: research the **official** paths by which an agent like Claude Code can
use its own model (MCP sampling, `claude -p`, Agent SDK), avoiding prompt-injection workarounds. And
decisively: **"this session is research only, no implementation."** The expected outcome asked for a
design document under `docs/`, a comparison table, source URLs and a named adoption candidate.

What the Do records is the Codex OAuth implementation. Twelve unit tests, 175 passing, config smoke
checked.

Good work. Not the planned work.

Study did not round it up. **`unmet`** — the only one in 22 cycles: "the expected `docs/` design
document, comparison table, source URLs and adoption verdict were not produced."

That is what judging **against the expectation rather than the output** does. And it is why the next
cycle got to exist.

### #13 — Write the provider survey and design docs

![Cycle 13](cards/cycle-13.svg)

Producing what #12 did not. Two subagents run in parallel — (a) a map of pdsa's LLM provider
architecture, (b) a survey of official Claude Code mechanisms — and the results become two documents:
`claude-code-self-llm-조사.md` (four candidates compared, with source URLs) and
`claude-code-provider-설계.md` (six insertion points, the factory-bypass trap, five open questions).

Four candidates were compared on cost, performance, integration, AOT, stability and immediate
feasibility, landing on **D conditionally adopted (ToS first), B as fallback, A rejected, C not
recommended**. No implementation — the plan-first rule. Verdict `met`.

The same subject, re-run with the document *as* the expected outcome, passed on the first try. The
difference was not what to build but **what would count as success**.

### #14 — Adopt `claude -p` as an LLM provider

![Cycle 14](cards/cycle-14.svg)

The design's P2 fallback becomes the formal choice. `ClaudeCli.cs` resolves the executable
(env > config > PATH) and `ClaudeCliClient` passes
`claude -p --output-format json --max-turns 1 --append-system-prompt` through `ArgumentList`, so
nothing needs escaping.

175→189 tests, zero warnings, and a real `claude -p` round trip returning `result=OK`. Verdict `met`.

No workaround was needed. One official headless CLI made "the agent uses its own model" true with no
key and no billing. But the round trip took **7,411ms**, and Act refused to let that pass —
reinforce `yes:` "immediately add user feedback and a timeout policy for the ~7.4s start-up delay."
That number becomes #18's plan four cycles later.

08:51, `f0a2eb0`. #13's two documents and #14's implementation rode in on the same commit.

<details>
<summary>The raw record for this chapter</summary>

- **#11** expected: 163 existing plus new Codex unit tests pass with zero warnings, and the user confirms
  login, refresh and an SSE response at least once with a real `auth.json` · **no verdict, actual or
  reinforce recorded** (cycle status `planned`) · the plan's implementation → `a2a1aaf`
- **#12** expected: at least one design document and one comparison table under `docs/`, covering support,
  auth, constraints, difficulty and source URLs per official path, with one adoption candidate named ·
  verdict **`unmet`** · actual: implementation, 12 tests, 175 passing and a config smoke confirmed; the
  expected documents, table, URLs and verdict were not produced · reinforce `yes:` have a Codex user run
  one real E2E · → `a2a1aaf`
- **#13** expected: a survey and a design document under `docs/`, comparing at least three providers on
  cost, performance, compatibility and security with an adopt/hold/reject verdict · verdict **`met`** ·
  actual: two documents written, four candidates compared on six criteria, D conditionally adopted,
  B fallback, A rejected, C not recommended · reinforce `no` · → `f0a2eb0`
- **#14** expected: 175 existing plus ClaudeCli tests pass with zero warnings, and after
  `config auth claude-cli` one real `claude -p` call returns a successful `result` with PDSA tags
  extracted · verdict **`met`** · actual: 189 passing, 14 new, zero warnings, a real 7,411ms round trip
  returning `result=OK` with tag extraction verified · reinforce `yes:` feedback and timeout for the
  7.4s start-up delay · → `f0a2eb0`
</details>

---

## Chapter 6 · The defects only use reveals

### #15 — Multi-project: `--project` on every command

![Cycle 15](cards/cycle-15.svg)

The Plan sentence is itself a bug report: "make `--project` run independently of global state.
**`ArgUtil.Positional` currently folds option *values* into the body and contaminates the record, so
this feature is in fact broken.**"

`Positional` collected every token not starting with `-` into the body. Type
`pdsa plan "add a cache" --project myrepo` and `myrepo` ended up inside the recorded text. A documented
feature was quietly broken, and that only shows up when someone **actually uses it**.

The fix was a value-option whitelist: skipping the token after any of eleven options (`--expect`,
`--project`, `--note` and the rest) fixed plan, do, study and guide at once.

Verdict `partial`, because concurrent `plan --project` across two projects was demonstrated but the
full do/study/act chain was not. Act refused "structurally guaranteed" as an argument and asked for a
demonstration.

### #16 — Reinforcement: close the gap #15 never demonstrated

![Cycle 16](cards/cycle-16.svg)

Eight minutes later, the reinforcement cycle. Two throwaway projects (`zz-fc-a`, `zz-fc-b`) run the
whole plan→do→study→act chain **concurrently** via `--project` (bash background jobs plus wait), and
each project's status phase lines get grepped for project-name tokens.

**Zero hits across all eight phase records.** Zero parallel collisions. Verdict `met`. The throwaway
DBs were deleted afterwards.

#15 had already fixed the code and its reasoning was right. All #16 added was **evidence**. That is why
`partial → reinforce → met` exists: an unproven claim is not rounded up to success, and it is not
thrown away either — it gets closed cheaply next cycle. 14:13, `a69e910` carries both.

### #17 — Run the viewer in-process for the AOT single file

![Cycle 17](cards/cycle-17.svg)

`pdsa view` worked perfectly in the dev tree and failed only from the npm install, with "viewer not
found". The cause was simple: it spawned a separate `AkkaGraphLoop.Viewer` exe, and an AOT single-file
distribution does not have one.

`ViewerHtml` moved into Core to be shared, and the CLI started a localhost server itself with
`System.Net.HttpListener`. JSON went through an STJ source-gen context for AOT safety. From the AOT
single `pdsa.exe` (40MB) all three routes answered 200 and 194 tests passed. Verdict `met`.

The lesson is that the distribution format creates design constraints. The moment AOT single-file was
the target, **every external-executable dependency became debt** — and reflection-based serialization
was out for the same reason.

### #18 — Timeout and spinner for the Claude CLI provider

![Cycle 18](cards/cycle-18.svg)

The 7.4 seconds measured in #14 became a plan two days later. With no timeout there was a hang risk,
and a silent wait looks like a freeze.

`ResolveTimeout()` and a pure `ParseTimeout` (env > config > 180s), a linked CTS that raises
`TimeoutException` (with tuning guidance) only when the internal token fires, and
`Kill(entireProcessTree)` on cancellation. Then `Cli/Spinner.cs` wrapped around six coaching call sites.

What was verified alongside it matters. The spinner writes to **stderr** and does nothing when
redirected — so the test pins **zero control characters in redirected stdout**. The constraint that a
UX improvement must not contaminate the output an agent reads was nailed down in the same cycle as the
feature. Verdict `met`. 22:49, `276eded`.

<details>
<summary>The raw record for this chapter</summary>

- **#15** expected: running p/d/s/a concurrently on two projects via `--project` leaves no option value
  in any body, stores every record only in the named DB, and keeps 100% of tests passing ·
  verdict **`partial`** · actual: concurrent `plan --project` with zero contamination, separate DBs,
  191 passing; the full chain was not run concurrently · reinforce `yes:` run the full chain in parallel
  and verify end to end · → `a69e910`
- **#16** expected: after a concurrent E2E all four phases are recorded, no record leaks the project-name
  value, and grep plus a DB query confirm placement · verdict **`met`** · actual: four phases recorded,
  grep for own and other project names across 8 phase inputs returned 0, zero parallel collisions ·
  reinforce `no` · → `a69e910`
- **#17** expected: on the AOT single file `view` starts localhost with no separate exe, three routes
  answer 200 under the camelCase contract, full suite passes · verdict **`met`** · actual: single
  `pdsa.exe` (40MB) started the server, three routes 200 and contract met, 194 passing ·
  reinforce `no` · → `e3cf27b`
- **#18** expected: precedence (env > config > 180s) verified by tests, process killed on timeout,
  spinner on an interactive terminal, zero spinner output in redirected stdout · verdict **`met`** ·
  actual: a 5-case Theory plus an E2E (env=1s overriding config=90s), a 1,175ms timeout with kill and
  guidance, TTY spinner and zero control characters when redirected · reinforce `no` · → `276eded`
</details>

---

## Chapter 7 · The memory starts compounding

### #19 — Structured `--json` output, recall, `--full`

![Cycle 19](cards/cycle-19.svg)

This is the project's second hinge.

Three things. (1) Opt-in `--json` structured output on seven commands — the contract that stops an
agent regex-scraping Korean coaching prose, serialized camelCase through source generation for AOT
safety. (2) `PdsaWorkflow.RecentLearnings` wired into `PdsaCoach.HypothesisAsync`'s `[past learnings]`
block, so `plan` injects the last three cycles' learnings into the coaching prompt **automatically**.
(3) `status/eval --full` to remove truncation.

(2) is the decisive one. Until then the graph was an **archive**: cycles accumulated and the next plan
never read them. After `recall`, those 85 small stars become the next Plan's input. The "compounding"
this document opens with is a story that starts exactly here.

204→213 tests, no prose regression, verdict `met`. And at 17:13 `f7bcef8` goes out on a branch, opening
**PR #1** — the first pull request in this repository. Everything before it went straight to main.

<details>
<summary>The raw record for this chapter</summary>

- **#19** expected: `--json` on seven commands verified against the camelCase schema through AOT
  source-gen, recall's learnings reaching `plan` coaching, `status/eval --full` untruncated, all tests
  passing · verdict **`met`** · actual: seven commands emitting valid JSON, source-gen, recall→plan
  injection, no truncation, 213 tests (204+9) and no prose regression · reinforce `no` ·
  → `f7bcef8` (PR #1)
</details>

---

## Chapter 8 · The loop audits itself

After a three-day gap, the last three cycles are the tool making **itself** the subject of the survey.

### #20 — Survey Akka.NET Streams and adjacent stacks

![Cycle 20](cards/cycle-20.svg)

A survey cycle: the unused Akka.NET Streams specs and adjacent stacks (OpenTelemetry, Polly, …) judged
on safety, efficiency, adoption cost and post-adoption checks. It dug up three things.

**First, the headline was idle.** Counting references to `IPdsaEngine` gave exactly one, at
`RunCommand.cs:13`. The Akka.Streams feedback cycle this repository is named after ran **only in the
demo** (`pdsa run`); the `plan/do/study/act` path people actually use was plain synchronous Kùzu calls.
So "let's adopt more Akka specs" reduced to "is an ActorSystem worth it in a 0.2–3s CLI", and the
answer was no.

**Second, `RetryFlow` does not exist.** Searching the nuget Akka.Streams XML directly:
`RestartFlow` 7 hits, `KillSwitches` 12, `Supervision` 113 — `RetryFlow` **0**. It exists in JVM Akka
and was never ported to .NET. The kind of fact that has to be looked up, not recalled.

**Third, failure injection reproduced a data-integrity defect.** Forcing an LLM failure in a throwaway
project left a phase-less **orphan cycle** (2/2, 100%). The cause was write order: `PlanCommand`
committed `StartCycle` **before** the LLM call. The second-order damage was worse — the next `do`
silently adopted that orphan and coached on "Plan: no input", leaving `expected` empty so Study could
not judge, which then polluted the hit-rate denominator and the quality of `recall`.

One transient network error, corrupting long-term memory. Verdict `met`, 01:10 `80e25ec`.

### #21 — Atomicity, bounded retry, per-phase metrics

![Cycle 21](cards/cycle-21.svg)

Implementing #20's three "now" items with **zero new dependencies**. The heart of this cycle is not
what went in but **the order it went in**.

The natural order was "retry first", and that was exactly wrong. Retry multiplies failed **attempts**,
and in a structure where every failed attempt leaves an orphan cycle, adding retry first would only
amplify the corruption. So atomicity (N1) comes first.

Before building `KuzuGraph.BeginTransaction`, **four tests checked whether transactions actually work
through the C API**. They passed — which made the prepared compensating-delete fallback unnecessary.
Resolving the uncertainty with a test instead of with code simplified the design. `StartCycleWithPlan`
wraps cycle, edges and plan phase in one transaction, and Do/Study/Act refuse a plan-less cycle.

Retry covers 429/5xx/socket/timeout only, at most twice, exponential backoff with jitter. 4xx and
cancellation propagate immediately. And `check` runs at zero retries — a diagnostic must never hide an
outage behind a retry.

Metrics reused the existing ALTER migration path for five columns, and token usage is collected through
an optional `ILlmUsageReporter` without touching the `ILlmClient` signature.

**Result: 20 injected failures, 0 orphans** (baseline 100%), 259 tests, AOT binary unchanged at 39MB.
Verdict `met`.

And this cycle's Study coaching quoted **its own metrics** (`latencyMs=22179`, 1286/1443 tokens) as
judging evidence. That metrics do not just get recorded but feed back into the verdict was demonstrated
on the spot.

One more thing came out sideways. A cold-start A/B showed a 45% improvement that did not exist; the npm
shell wrapper was adding ~190ms to the baseline. The number was corrected in the record rather than
kept — **the measuring instrument is part of the system.**

### #22 — `history` and `show`: query the loop's own past

![Cycle 22](cards/cycle-22.svg)

The last cycle came out of **writing this document**.

Assembling 22 cycles of narrative, the data could not be got out through the CLI. `status` is a
newest-first dump, `eval` gives only expected/verdict/actual, `recall` only learnings. There was no way
to address one cycle, read the timeline oldest-first, or slice a range, and `REINFORCES` was visible
only in the viewer. It came down to parsing the graph JSON by hand.

That being exactly the kind of gap the loop should surface, it became a cycle: `pdsa history` (an
ascending timeline) and `pdsa show <n>` (one cycle in full, with metrics and both directions of the
reinforcement link).

One implementation judgement is worth keeping. Adding `Cycle(id)`, `Range(from, to, asc, limit)` and
`ReinforceLinks(id)` to Core came with making a private **`Fetch()` the single retrieval path**, with
`Recent()` reimplemented on top of it. With no per-command query there is nothing to drift, and a
structural test asserts all three APIs agree on the same cycle. However many commands get added,
disagreement is **structurally impossible**.

271 tests passing, verdict `met`. And this document's narrative was extracted from CLI output alone —
zero direct JSON parses, against a baseline of one.

<details>
<summary>The raw record for this chapter</summary>

- **#20** expected: a `docs/` document covering 100% of surveyed technologies with usage evidence, a
  four-lens evaluation, verification methods and now/next/later/reject verdicts, with quantitative
  metrics for at least one "now" candidate · verdict **`met`** · actual: all 20 candidates covered, five
  quantitative metrics defined · reinforce `yes:` make `PlanCommand`'s write order atomic so a cycle is
  created only after the LLM result is verified · → `80e25ec`
- **#21** expected: 20 injected failures leave zero orphans, retryable errors retried at most twice,
  a new cycle's Study input carries metrics, no regression in tests, cold start or binary size ·
  verdict **`met`** · actual: zero orphans, retry policy pinned, metrics recorded and injected,
  259 tests, 39MB+13MB and 229–237ms with no regression · reinforce `no` · metrics: do 22,179ms ·
  study 11,063ms · act 4,175ms · → `a2fbf08`, `d4c882d` (PR #4)
- **#22** expected: against the real 21-cycle DB, `show` and `history` return each cycle's
  expected → verdict → actual → learning, metrics and REINFORCES correctly, with range, ordering, JSON,
  legacy-schema compatibility and the full suite passing, and the narrative writable from CLI output
  alone · verdict **`met`** · actual: 21 cycles returned ascending, both REINFORCES directions, boundary
  cases and legacy-schema compatibility, 271 tests, narrative extracted from the CLI · reinforce `no` ·
  metrics: plan 6,659ms · do 19,233ms · study 9,003ms · act 4,990ms · → `d750894`, `f0e42cc` (PR #4)
</details>

---

## Interlude · The commits that never rode the loop

One thing worth stating plainly: **not every commit went through a cycle.**

The 22 cycles landed in 12 commits. Roughly fourteen others shipped with no cycle at all:

| Commit | What |
|---|---|
| `0f69455` | npm release CI (AOT, three platforms) |
| `9559421` `8b11f89` `fbcc4c0` | English READMEs, hero images |
| `493a386` | the PDSA history and theory essay |
| `f16eccb` | settle the viewer with alpha cooling |
| `8be1413` | bump CI actions to Node 24 |
| `d42a710` `e25f9a0` `a863721` | the Astro product site |
| `743ab0a` | force UTF-8 console output on Windows (PR #2) |
| `553ba12` | the `update` command and version notice (PR #3) |
| `8271179` `0feb30b` | documentation updates |

A pattern shows. **The loop was used for uncertain work and skipped for certain work.** Auth expansion,
self-model providers, the atomicity defect — everything that needed "what counts as success" nailed
down in advance ran as a cycle. Bumping a CI version or fixing a typo just got committed.

That is not a failure of the loop. **Where to apply PDSA is itself something this project learned.**

---

## Five things that run through all 22

**1. Writing the expected outcome first is what makes improvement measurable.**
Cycles 1–3 produced real work and left no way to tell whether it succeeded. The verdict field is what turned
"we did things" into "we predicted 18 outcomes and hit 11."

**2. `partial` is the useful verdict.** Seven of the eighteen judged cycles were not clean successes
(six `partial`, one `unmet`). Each named a specific unproven claim, and three of them — #4, #12 and #15 — were
closed by an explicit reinforcement cycle rather than being quietly forgotten.

**3. Dogfooding finds what design reviews miss.** The orphan-cycle defect in #21 and the missing query commands in
#22 were both found by *using* the tool, not by reading the code. So was #15's parsing defect.

**4. Order matters more than the individual fixes.** #21's central finding was not "add retry" but "do not add retry
before atomicity". #7's "path seam before integration test" is the same shape of lesson.

**5. The measuring instrument is part of the system.** #21's cold-start comparison showed a 45% improvement that did
not exist; the npm shell wrapper was adding ~190ms to the baseline. The number was corrected in the record rather
than kept.

---

## Reading the raw record yourself

Nothing here is a summary you have to trust — every cycle is queryable:

```bash
pdsa history --project akka-graph-loop                # timeline, oldest first
pdsa history --from 15 --to 16 --full                 # one reinforcement pair, untruncated
pdsa show 21 --full                                   # a cycle's four phases, metrics, links
pdsa history --json | jq '.cycles[] | {id, verdict}'  # machine-readable
pdsa view                                             # the graph itself, in a browser
```

Cards are generated from the same records by [`cards/_generate.py`](cards/_generate.py)
(English in `cards/`, Korean in `cards-ko/`).

---

# The loop's compounding becomes a constellation

After 22 cycles this project's memory has a **shape**, not just a document. What follows is not a
metaphor — it is the graph. Every star is a node and every line is an edge, drawn from the **same
data** `pdsa view` renders in a browser, with the same force-directed layout.

![The loop's compounding becomes a constellation](constellation/constellation.svg)

## How to read it

| What you see | What it is in the graph | Count |
|---|---|---|
| the bright core at the centre | the `Project` node — the origin of all this memory | 1 |
| the very faint threads leaving it | `HAS_CYCLE` — the project owning each cycle | 22 |
| the heavy line forming the ring | **`NEXT`** — the **spine** chaining cycles in time | 21 |
| the large stars (shape and color = verdict) | the 22 `Cycle` nodes | 22 |
| the small stars clustered around each | `Phase` — every cycle's Plan, Do, Study and Act | 85 |
| the hairlines reaching them | `HAS_PHASE` | 85 |
| the orange dashed arcs bending backwards | **`REINFORCES`** — loops back to an unclosed gap | 5 |

**108 nodes, 133 edges** in total. One star per cycle with four small ones attached, threaded into a
ring by `NEXT`, and cut across by `REINFORCES` running the other way.

## Why this shape *is* the compounding

A linear record would draw a **straight line** — 22 dots in a row. Three things bend it into this.

**1. Each cycle contains its own four phases.** A cycle is not a point, it is a **cluster**. Those 85
small stars are the material `recall` later reads back, and the per-cycle P/D/S/A above *is* them.

**2. `REINFORCES` runs against time.** The five orange arcs are the only **backwards** edges.
`#15 → #16` is the trace of a claim being sent back to be proven; `#12 → #13` is the only `unmet`
being paid off. Without these arcs the graph is a chain; with them it is a **web**.

**3. The core holds it all together.** Those 22 faint threads say this memory is isolated per project.
Run another project and an entirely different constellation forms in its own database.

Compounding means **later work uses earlier work as input**, and the graph carries two kinds of it.
`REINFORCES` — the arcs bending back — has been there since #5, but it is the **coarse, judged**
feedback: a person decided the gap was still open. The `recall` that #19 attached is the fine one,
feeding all 85 small stars into the next Plan's coaching **automatically**. Before #19 those small
stars were an archive; after it they were an input.

## What the constellation shows at a glance

- **One red square, lower left** — `unmet` happened once in 22 cycles, at #12. An orange arc leaves it
  straight to #13. That the failure was not abandoned is visible as shape.
- **The hollow rings inside the ring** — `#1`, `#2`, `#3` and `#11` are open circles. Cycles with no
  verdict — everything before the closed loop, plus the one abandonment — are separated by **glyph**,
  not by color alone.
- **The arcs cluster on one side** — reinforcement happened in Eras 3, 5, 6 and 8, and never in Era 4
  (#9, #10) or Era 7 (#19). Which stretches closed in one pass and which took two is legible from the
  shape.

> To turn it yourself, run `pdsa view` — the same graph opens in a browser, draggable.
> The image is generated by [`constellation/_generate.py`](constellation/_generate.py) from
> [`graph-snapshot.json`](constellation/graph-snapshot.json), the viewer API response verbatim.

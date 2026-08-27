# PDSA — History, Theory, and the Quality Legacy

**English · [한국어](PDSA-ko.md)**

![PDSA — Plan · Do · Study · Act; Study, not Check](docs/pdsa-hero.png)

> Why a code repository ships a history essay: this project records work as **PDSA cycles** and accumulates
> them as long-term memory for an AI agent (see [`README.md`](README.md)). The idea behind that loop is not a
> throwaway acronym — it is a decades-long story about **quality**, and it sets the direction this project
> means to keep improving toward. This page summarizes that story. Every claim below is fact-checked against
> the sources listed at the end; where popular retellings get details wrong, the correction is noted inline.

---

## 1. The root: build quality *in*, don't inspect it *out*

In the 1920s–30s at Bell Labs, **Walter A. Shewhart** developed statistical quality control and the iterative
improvement loop later known as the **"Shewhart Cycle."** His central shift: aim for **uniform results during
production** rather than catching defects by inspection at the end of the line.

**W. Edwards Deming** worked with Shewhart in the 1930s and carried this further — from statistics on the shop
floor to a whole philosophy of management. PDSA is a direct descendant of Shewhart's cycle, refined by Deming.

## 2. Volume first, quality second — and its price (Atari)

Early mass production optimized for **output and cost**. When quantity and speed are prioritized and quality
is treated as an afterthought, the bill eventually arrives. The clearest cautionary tale is the
**video game crash of 1983** in North America:

- The market was flooded with too many consoles and **hundreds of mostly low-quality games**. Overproduction
  outran demand — in 1983, demand was up ~100% year over year while manufacturing output rose ~175%.
- Console makers had **no quality control over unlicensed developers**, so "shovelware" piled up.
- Home video-game revenue collapsed from about **$3.2 billion (1983) to roughly $100 million (1985)** — a
  drop of nearly **97%**.
- The emblem of the era: Atari's **_E.T. the Extra-Terrestrial_** (1982), rushed to market in about five to six
  weeks. Around **5 million cartridges were made and only ~1.5 million sold**; Atari buried an estimated
  **~800,000 cartridges** in a landfill in **Alamogordo, New Mexico (September 1983)**. The burial was long
  treated as an urban legend until it was **excavated in April 2014**, recovering ~1,300 cartridges.

The lesson is not "video games were a fad." It is that **quantity without quality destroys trust**, and trust
is the market.

## 3. Deming goes to Japan (1950)

Invited by the **Union of Japanese Scientists and Engineers (JUSE)**, Deming arrived in Japan in **July 1950**.
Over roughly 68 days he delivered lectures including an eight-day quality-control course and a **course for top
management at Hakone**. He taught statistical process control *and* the management philosophy for which he
later became famous. Many credit Deming as one of the inspirations for Japan's **post-war economic miracle**.

### The Deming Prize

Deming donated the royalties from his 1950 lecture transcripts to JUSE. In gratitude, JUSE's board
**established the Deming Prize (December 1950; first awards September 1951)** — still the most prestigious
quality award in Japan.

## 4. Japan practiced it first — Toyota and beyond

**Toyota** introduced Total Quality Control (TQC) in **1961** and **won the Deming Prize in 1965**. Principles
it embraced — continuous improvement, respect for the worker's knowledge of the process, and relentless
elimination of waste — became part of the philosophical foundation of the **Toyota Production System (TPS)**.

> ⚖️ **Balance, not a single cause.** TPS is also credited to Taiichi Ohno and the Toyoda family, and Japan's
> rise across **autos, electronics, and semiconductors** had many drivers (industrial policy, investment,
> engineering culture). Quality was a **major factor, not the sole one.** But that a quality-first culture took
> root in Japan first is not in dispute.

## 5. Nintendo's counterattack — quality revives a "dead" market

After 1983, many declared the console business finished in North America. The revival came from Japan:
Nintendo's Famicom (Japan, 1983) launched in North America as the **NES in October 1985**. What made it work
was, pointedly, **quality control** — the exact thing whose absence had killed the previous generation:

- The **Nintendo Seal of Quality**: no game could ship without passing Nintendo's approval.
- **Strict third-party licensing** and a hardware **lockout chip (10NES)** that blocked unauthorized
  cartridges — a legally and technically enforced gate on what could be published.
- The strategy was **"fewer, better games,"** anchored by flagships like **_Super Mario Bros._ (1985)**.

Nintendo's revival is the mirror image of Atari's collapse: where unchecked quantity had destroyed confidence,
**enforced quality rebuilt it.**

## 6. The West re-learns quality

The quality theory was **born in the United States** (Shewhart, Deming, and peers such as Joseph Juran) — yet
it was **practiced first, and most completely, in Japan.** The West caught up later:

- **NBC** aired **"If Japan Can... Why Can't We?"** on **June 24, 1980** (an NBC White Paper). It is credited
  with launching the American "Quality Revolution" and reintroducing Deming to U.S. managers; demand for his
  consulting surged afterward.
  > 📌 **Correction to a common retelling:** this documentary was **NBC**, *not* the BBC.
- The U.S. established the **Malcolm Baldrige National Quality Award** (by the act of **1987**); Europe followed
  with the **European Quality Award / EFQM (1991)**. Both were explicitly modeled on Japan's **Deming Prize**.

For most of his life Deming was, in the phrase often used, **"a prophet without honor in his own land"** —
celebrated in Japan long before his own country embraced him. He is widely regarded as one of the **fathers of
the modern quality movement.**

## 7. The final correction: it's PDSA, not PDCA

The most important detail — and the reason this project uses **PDSA** — is a correction Deming himself insisted
on. The cycle is often taught as **PDCA (Plan-Do-Check-Act)**, but Deming emphasized **PDSA
(Plan-Do-Study-Act)**, with the third step as **Study**, not **Check**.

In **1986**, Deming warned that the English word **"check" means "to hold back,"** and he called the PDCA
version a **"corruption."** The difference is philosophical, not cosmetic:

| | **PDCA** (Check) | **PDSA** (Study) |
|---|---|---|
| Third step | **Check** — did it pass? | **Study** — what did we learn? |
| Orientation | Verify implementation (success/failure) | Predict → observe actual → compare → revise the theory |
| Variation | Often ignored | Seeks the **sources of variation** |
| Loop's purpose | Conformance | **Learning that improves the process itself** |

PDCA asks *"did it work?"* PDSA asks *"what did we learn, and how should that change our theory?"* The second
question is what compounds into knowledge over many cycles.

## 8. What this means for this project

`pdsa` (this repo's CLI) is built on the PDSA reading deliberately:

- **Plan** sets a *verifiable expected outcome*, not just a task list.
- **Study** records a **verdict** (met / partial / unmet) and what was learned — it is **Study, not Check**.
- **Act** carries the learning forward; if reinforcement is needed, the next cycle links back to it.
- Every cycle accumulates into a **per-project graph memory**, so the *process itself* improves over time.

That is the direction: not "did the task pass," but **"what did we learn, recorded so the next cycle is
smarter."** Deming's last correction, applied to an AI agent's memory.

---

## Sources

- W. Edwards Deming Institute — [PDSA Cycle](https://deming.org/explore/pdsa/) · [*If Japan Can, Why Can't We?* (1980, NBC)](https://deming.org/if-japan-can-why-cant-we-1980-nbc-special-report/)
- ASQ — [The Legacy of W. Edwards Deming](https://asq.org/quality-progress/articles/the-legacy-of-w-edwards-deming)
- JUSE — [How the Deming Prize was established](https://www.juse.or.jp/deming_en/award/) · [History of the Deming Prize](https://www.juse.or.jp/deming_en/award/01.html)
- Wikipedia — [Deming Prize](https://en.wikipedia.org/wiki/Deming_Prize) · [If Japan Can... Why Can't We?](https://en.wikipedia.org/wiki/If_Japan_Can..._Why_Can't_We%3F) · [Video game crash of 1983](https://en.wikipedia.org/wiki/Video_game_crash_of_1983) · [PDCA](https://en.wikipedia.org/wiki/PDCA) · [Malcolm Baldrige National Quality Award](https://en.wikipedia.org/wiki/Malcolm_Baldrige_National_Quality_Award)
- GeekWire — [Atari *E.T.* cartridges excavated (Alamogordo, 2014)](https://www.geekwire.com/2014/atari-cartridges-dug/)
- Ronald Moen — [Foundation and History of the PDSA Cycle (PDF)](https://www.praxisframework.org/files/pdsa-history-ron-moen.pdf)

> Accuracy note: only fact-checked claims are included. Interpretive framing (e.g., "quality was a major but not
> the sole cause" of Japan's industrial rise) is flagged as such. Corrections to common misretellings — the
> 1980 documentary being **NBC** rather than the BBC — are marked inline.

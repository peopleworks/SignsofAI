# A procedure for when the question becomes formal

For an academic-integrity committee, a programme director, or a teacher who has to write something
down. It assumes the hardest case: the student denies it.

**The one rule the rest depends on:** a detector's output is never evidence of authorship, and no
part of this procedure treats it as such. What follows is how to reach a defensible decision anyway.

---

## Sort what you have into three kinds

Most flawed integrity cases collapse because these were mixed together in one paragraph.

**1. Checkable facts.** Statements about the document that are true or false and that anyone can
verify independently — this citation appears in the text and nowhere in the reference list; these two
references carry the same DOI, so at most one of them is right; this reference is dated next year;
this word contains a Cyrillic letter shaped like a Latin one.

These do not depend on trusting any tool. **Anyone can check them by hand**, and a committee should:
open the bibliography, look. They also carry no opinion about authorship — a real source can be
produced in seconds, and that request is the whole test.

**2. A pattern score.** An opinion about prose. Useful for deciding where to read carefully. **It is
not evidence and it belongs in no finding.** If your written decision would be weaker with this
paragraph removed, the decision is not sound.

**3. What the student says about their own work.** In practice this decides the case, and the rest of
this document is about getting it right.

## Before the meeting

**Verify the checkable facts yourself.** Look up the DOIs. Search the reference list. If a claimed
contradiction does not hold when you check it by hand, drop it — one wrong item in a written finding
is enough to void the whole document at appeal, and rightly.

**Write down what specifically raised the question**, in one or two sentences a student could
understand. If you cannot, there is no case yet. *"The score was high"* is not a reason. *"Four of
the six sources cited do not appear in the bibliography, and two share a DOI"* is.

**Check the tool's error rate for the language the work is written in.** This matters more than it
sounds. This tool's corpus supports **a threshold for English on its own (30/100, upper bound 2.7%)
and none for Spanish**, whose 25 texts bound it no tighter than 13.3% — against a pooled figure of
2.4% that is mostly English. Quoting the pooled figure, or the English one, at a student writing in
Spanish is the exact error the tool's own documentation warns against. If you cite a figure in a finding, cite the one for that language, and
if there is none, say there is none.

**Decide who is in the room.** For the student: someone they choose. The power difference is the
main source of unfair outcomes in these meetings, and it is cheap to reduce.

## The meeting

**Open by saying what it is.** *"This is a conversation about your assignment, not a decision. Nothing
has been decided."* Say it even if you think it is obvious. It is not obvious from the other chair.

**Ask about the work, not about the accusation.** Take two or three specific passages and ask:

> *You wrote this phrase here — what does it mean, in your own words?*
>
> *Why this example and not another one?*
>
> *What did you leave out of this section, and why?*
>
> *Where did this source come from? What does it actually argue?*

Someone who wrote a text can talk about the choices in it — not perfectly, not fluently, but they can
say why. Someone who did not write it produces summary rather than intention: they can restate the
paragraph and cannot say why it is that paragraph. **This asymmetry is the strongest instrument in the
room, and it is free.**

**Ask for the sources.** Not the citations, the sources. A real one arrives in seconds. An invented
one cannot arrive at all, and the failure to produce it is a fact you may write down.

**Ask for the drafts, but weigh their absence carefully.** Drafts are strong evidence when present
and weak evidence when missing. Plenty of honest students write in one pass, in one file, and delete
nothing because there was nothing to delete. Absence of drafts is not evidence of anything, and a
policy that treats it as such punishes people for how they work.

**Take the answers seriously when they explain things.** *"I write formally because that is how I was
taught"* is a complete explanation for a high score, and it is a common one among students writing in
a second language. A committee that has decided in advance what the meeting will conclude is not
holding a meeting.

## Writing the decision

The document should be able to stand with the detector removed from it entirely. Test that literally:
delete every sentence about the tool and read what is left. If a decision remains, write it. If
nothing remains, there is no finding.

**State the facts you verified yourself** and how you verified them. **State what the student said.**
**State what you concluded and why.** If you mention the tool at all, say what it is and what it is
not, in the finding itself:

> A writing-analysis tool was used to decide which sections to examine. Its output is not evidence of
> authorship and was not treated as such. It publishes a false-positive rate on writing known to be
> human, measured for the language this work is written in and printed on the report itself — the
> findings below rest on the source verification and the interview, not on that tool.

**Copy the figure from the report in front of you, not from here.** The tool measures a separate rate
per language and the report prints the one that applies to the work being judged. Quoting the pooled
figure instead is the single easiest way to overstate this tool at a hearing: on the current corpus
the pooled rate is under 2.4%, while Spanish on its own supports only 13.3% — five times worse. A
committee handed the flattering number about a Spanish essay has been given a better tool than the
one that was actually used, and the difference is the sort of thing that surfaces on appeal.

A committee that writes this sentence is in a stronger position than one that omits it, because the
sentence is going to be raised at appeal whether or not it appears.

## Proportion

Most of what these processes catch is not fabrication. It is a student who was overwhelmed, used a
tool for a paragraph, and did not disclose it because nobody told them how. A first response of
*rewrite it and tell me what you used* keeps a student in the room and produces a better writer. The
severe outcomes should be reserved for what deserves them: invented sources, work bought or taken
from another person, and denial maintained against facts the student cannot explain.

---

## What this project will not give you

**A confidence percentage.** It does not have one, and neither does anyone else who prints one.

**A verdict below its supported threshold.** The report prints the number and no interpretation.
That is deliberate: a page that says *reads mostly human* above *treat this score as saying nothing*
lets the reader keep whichever half they came in wanting.

**A false-positive rate for your student population.** The published figure was measured on articles
published before generative models existed — not on first-year coursework, not on your institution,
not on your language mix. It is a floor, not a promise. Anyone quoting it as though it applied
directly to a nineteen-year-old's essay is overstating it, including us.

---

Related: [`syllabus.en.md`](syllabus.en.md) — policy language before any of this is needed.
[`student-sheet.en.md`](student-sheet.en.md) — what students should know beforehand.
[`../CALIBRATION.md`](../CALIBRATION.md) — the error rate and its method.

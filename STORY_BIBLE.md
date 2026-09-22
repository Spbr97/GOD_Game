# STORY BIBLE — THE GOD WHO WAS FORGOTTEN

Required by SPEC.md section 82. This is the authoritative record of the game's
fiction. SPEC.md remains the authoritative record of its *requirements*; where
the two disagree about a story fact, SPEC.md wins and this file is wrong and
must be corrected.

## How to use this file

SPEC.md section 82 says, in full: **"Do not allow AI coding agents to invent new
canon casually. If a new story element is needed, propose it first and update
the story bible."**

That rule has a practical shape:

1. **Before writing any character line, item description, place name, memory
   text or inscription, read this file.** If the thing you need is already here,
   use it exactly as written.
2. **If it is not here, it does not exist yet.** Propose it — in the task's
   response, in one or two sentences, saying what you want to add and why the
   task cannot be done without it — and add it to [Canon added during
   implementation](#canon-added-during-implementation) in the same change.
3. **Never contradict [Canon from SPEC.md](#canon-from-specmd).** That section
   is transcribed from the specification and is not yours to revise. If it needs
   changing, SPEC.md changes first.

Everything in [Canon added during implementation](#canon-added-during-implementation)
was written to fill a gap the spec left open. It is all **provisional** and may
be revised or dropped; it is recorded here precisely so that decision is
possible. Nothing in it should be treated as settled merely because it shipped.

---

# CANON FROM SPEC.md

Transcribed from SPEC.md sections 4–11. Authoritative.

## Mythology

Thousands of years ago humanity lived under the protection of **seven gods**,
each governing one fundamental force:

| God | Force | Region | Ability granted | Boss |
|---|---|---|---|---|
| Agniya | Fire, destruction, purification | The Burning City | Ember Step | The Flame Sovereign |
| Varuna | Water, depth, the forgotten dead | The Drowned Palace | Tide Veil | The Leviathan of the Deep |
| Vayu | Wind, freedom, movement | The Sky Kingdom | Wind Leap | The Storm Serpent |
| Dhara | Earth, mountains, stone, endurance | The Buried Kingdom | Earth Break | The Stone Colossus |
| Surya | Light, truth, heat | The Endless Desert | Solar Sight | The Sun-Eater |
| Chandra | Dreams, illusion, night | The Dreaming Forest | Moonwalk | The Dream Queen |
| Kaal | Time, decay, destiny | The Temple Outside Time | Moment Break | The Clockless King |

There was an **eighth** divine being: **Nirvaan, god of Memory**. He ruled no
physical element — he ruled remembrance. He believed humanity should eventually
exist without divine control. The seven considered this dangerous, and because
they could not kill him, they erased him: his temples destroyed, his name gone,
his followers' memory of him removed.

The erasure damaged reality. Thousands of forgotten people were left trapped
between life and death — **the Forgotten**.

Nirvaan's symbol is **a closed eye surrounded by eight broken rings**.

## The First Memory

A cosmic entity that existed **before** the gods. It feeds on memory and uses
civilisations as a source of its own existence. Nirvaan discovered it; the seven
erased him because they believed removing his memory would stop humanity
discovering the truth. They were wrong.

It is not a humanoid villain. It is an enormous floating eye among fragments of
ancient statues, thousands of floating memories, human silhouettes, broken
temples and cosmic darkness, threaded together with gold. It exists outside
normal time and its voice is layered, ancient and non-human.

## Characters

### Ishan — protagonist
A young warrior of Avarsha, and the reincarnation of Nirvaan. Determined,
curious, protective; loyal to Avarsha at first and increasingly unsure of who he
is. His arc across the five acts:

- **Act I** — "I must protect my kingdom."
- **Act II** — "The kingdom has lied to me."
- **Act III** — "The gods have lied to humanity."
- **Act IV** — "I may be one of the gods."
- **Final Act** — "Do I preserve the world, the gods, or myself?"

### Queen Amara — leader of Avarsha
Introduces the political world and gives Ishan his early missions. Represents
humanity's dependence on divine protection.
**Secret:** she has been receiving memories from Nirvaan in her dreams.

### Dev — Ishan's childhood friend
Emotional anchor, combat companion, comic relief early on, and the clearest
demonstration of what memory loss costs.
**Potential tragedy:** Ishan eventually forgets Dev.

### Mira — a historian
Explains ancient inscriptions and guides the player toward the temples. Knows
more about Ishan than she admits.
**Twist:** Mira is one of the Forgotten — a person erased from history.

### The Archivist — masked supernatural figure
Recurring antagonist. Observes Ishan, manipulates events, gives contradictory
information.
**Truth:** a previous manifestation of Nirvaan who failed to break the cycle.

## Timeline

1. **The age of the seven.** Humanity under divine protection. Eight gods, one
   of whom rules memory.
2. **Nirvaan's discovery.** He finds the First Memory and concludes humanity
   must eventually stand without gods.
3. **The erasure.** The seven remove him from collective memory. His temples
   fall, his name goes. Reality is damaged; the Forgotten are made.
4. **Centuries pass.** The erasure holds. Histories record seven gods.
5. **The night without dreams (Act I).** Everyone in Avarsha falls asleep at
   once. Only Ishan stays awake. The statues of the seven bleed and a voice
   says: *"They erased my name. But they could not erase my memory."*
6. **The seven temples (Act II).** Each holds part of the truth and grants one
   divine ability. Every mark costs memory.
7. **The Forgotten (Act III), The God With No Name (Act IV), The First Memory
   (Act V).** See SPEC.md section 11.

## Themes

Memory, identity, free will, sacrifice, power, history, truth, mortality;
whether gods should rule humanity; whether a person remains themselves after
losing their memories. Secondary: the reliability of history, generational
trauma, the cost of power, and the difference between being remembered and being
alive.

---

# CANON ADDED DURING IMPLEMENTATION

**Provisional.** Everything below was written during TASK 003–018 to fill a gap
SPEC.md left open, and is recorded here so it can be reviewed, revised or
dropped. It was not approved in advance, which is the rule this file exists to
start enforcing.

## Terminology

| Term | Meaning | Where it came from |
|---|---|---|
| **The ruin stone** | A carved stone in the ruins beyond Avarsha's temple district bearing the seats of the gods. It shows **eight** seats; the eighth name has been ground away. | TASK 003. The spec requires Act I to establish the missing eighth god; this is the physical object that does it. |
| **The Forgotten** | The people trapped between life and death by the erasure. Used as a proper noun. | SPEC.md section 4 describes them; the capitalised name is ours. |
| **Divine Mark** | The mark a temple leaves on Ishan, and the item id `DIVINE_MARK`. | SPEC.md section 4 says "every divine mark has a cost"; using it as a noun for the item is ours. |
| **Astra Blade** | Ishan's starting weapon (`ASTRA_BLADE`). | TASK 002. Placeholder name for the placeholder weapon. |
| **Ember Draught** | A consumable that restores health (`EMBER_DRAUGHT`). | TASK 015. Named for Agniya's domain because the slice's temple is hers. |

## Geography of Avarsha

The hub city, built for TASK 003 and described only in outline by SPEC.md
section 24. Districts, in the order the player meets them:

- **The City Gate** — where the player enters and where Act I opens.
- **The Royal Palace** — Queen Amara.
- **The Marketplace** — Dev's post.
- **The Training Ground** — combat practice; Dev's thirty laps.
- **The Temple District** — statues of the seven, and the three-brazier fire
  puzzle. "Where the ground stops being ours" is just past it.
- **The Library** — Mira.
- **The Residential District**.
- **The Ancient Ruins** — the ruin stone, the first memory fragment, and the
  ambush that follows taking it.
- **The Mini-Boss Arena** — the Temple Guardian.
- **The Agniya Temple** — the slice's temple section, its four-brazier puzzle,
  its memory toll and Agniya's boss arena.

## Characters as written

Ishan does not speak on screen in the vertical slice. The three NPCs do.

### Queen Amara
Written as someone whose authority makes her precise rather than grand. She
reports the dream as an administrative problem and is most unsettled by the fact
that the histories are *not* wrong.

> "Ishan. Good. I have had the same dream four nights running, and I am told
> that is not a thing queens are permitted to mention aloud."

> "In it, there are eight thrones on the ruin stone past the temple district.
> Our histories record seven gods. I would like you to go and count them for
> me."

> "No. They are not wrong. That is exactly what troubles me. A wrong history can
> be corrected. A history that is complete and still short one seat is something
> else."

> "Go quietly. Take nothing from the stone but what it says. And Ishan — if you
> find you already know what is written there, come back faster."

> "Then we have both dreamed it. Ishan, I want you to listen carefully, because
> I am about to say something I will not be able to repeat, and I do not know
> why I am so certain of that —"

That last line is cut off deliberately. It is the slice's clearest
demonstration of Nirvaan's domain acting on a person mid-sentence, and it
delivers Amara's SPEC.md secret (she is receiving his memories) without stating
it.

### Dev
Comic relief that carries the theme rather than sitting beside it. His joke is
that he counts things; the horror is that counting is exactly what nobody in
Avarsha has done.

> "There he is. Thirty laps of the training ground before sunrise and he still
> walks like he is being marked on it."

> "I count everything. It is the only thing I am better at than you, and I
> intend to keep it."

> "The old ruins? Ishan, nobody goes out there. Not because it is forbidden.
> Because nobody thinks of it. I have lived here my whole life and I have never
> once thought of it. Does that not strike you as —"

### Mira
Written as a scholar whose discipline has been quietly defeated by the erasure,
and who knows it.

> "You are the Queen's soldier. Do not look startled, it is not insight, you are
> the only person in Avarsha who stands like that."

> "The ruin stone. Yes. I have copied that carving nine times and I have nine
> different counts, and I am not a careless woman."

> "Seven, eight times. Eight, once. I threw that page away, which was the single
> least scholarly act of my life, and I could not tell you why I did it."

> "Go and look. And Ishan — whatever you read there, say it out loud before you
> walk away from it. Things last longer when they are spoken."

> "You said it out loud, did you not. Good. Then there are two of us who cannot
> quite repeat it, and that is one more than there was yesterday."

Mira's SPEC.md twist — that she is one of the Forgotten — is **not** revealed in
the slice. Her nine contradictory counts are the seed for it.

The Archivist does not appear yet.

## The memory fragments

Three exist. Their texts are canon because a memory fragment *is* its text.

**MEM_001 — The Name Beneath the Stone** (critical; cannot be corrupted)
> The ruin stone carries eight seats, not seven. Someone has ground the eighth
> name away, patiently, with a blade — not to deface it, but to finish it.
> Underneath the damage the carving is still legible in the way a scar is
> legible. You do not know the name. You are certain you have said it before.

**MEM_002 — The Guardian's Vow**
> The stone that carried it is broken open now, and what was sealed inside is a
> memory that is not quite yours and not quite anyone's — a vow spoken by
> something built to keep one thing safe for longer than the word "forever" was
> supposed to mean. It does not know its vow is finished. It only knows it is
> not moving anymore.

**MEM_003 — Agniya's Ember**
> The flame does not go out when the god falls. It just stops pretending to be a
> god and remembers being fire — patient, indifferent, older than the seven,
> older than the war that made them seven. Somewhere in the heat you were sure
> you heard it say your true name before it forgot how.

MEM_003 contains the slice's only forward reference to the First Memory
("older than the seven, older than the war that made them seven"). "The war that
made them seven" is **new canon and the largest single invention in this file**:
it implies the seven's number was an outcome, not an origin. It is consistent
with SPEC.md section 4 (there were eight) but the spec never calls the erasure a
war. Flagged for review.

## Nirvaan's three lines

Spoken in `Cinematic_NirvaanSpeaks`, the Act I cinematic:

> "You carry a name older than this city's stones. Older than mine, and I am the
> one who forgot it."

> "They will tell you a god sleeps beneath Avarsha. They are wrong. A god is
> remembering."

> "Find what was taken from you. Before it finds you first."

The first line is doing something the spec does not require and should be
checked: it implies Nirvaan has also forgotten the name, which sets up the
Archivist (a previous manifestation who failed) rather than a straightforwardly
omniscient guide. The third line's "it" is the First Memory.

## Quests

- **Q001 — The Queen's Charge.** Search the ruins beyond the temple district,
  recover what the ruins remember, return to Amara. Rewards: Amara's trust.
- **Q002 — The Ash at the Gate.** Something crawled out of the ruins behind you.
  Put it down. (Three enemies.)

## Bosses named so far

- **Temple Guardian** (`MINIBOSS_TEMPLE_GUARDIAN`) — the mini-boss in the ruins
  arena. Its defeat yields MEM_002. Not a spec-named boss; invented for the
  slice.
- **Agniya, the First Flame** (`BOSS_AGNIYA`) — the Agniya temple boss. **Note a
  conflict:** SPEC.md section 8.1 names Agniya's boss *The Flame Sovereign*. The
  slice fights the god under her own name with the epithet "the First Flame".
  Either the temple boss should be renamed to The Flame Sovereign, or the bible
  should record that the Flame Sovereign is a later, separate encounter. **This
  needs a decision.**

## Dialogue rules

Rules the written dialogue follows. New dialogue should follow them too.

1. **Ishan never speaks on screen.** The player chooses; the NPC answers. This
   keeps Ishan's identity an open question, which is the whole plot.
2. **Nobody explains the theme.** No character says "memory is unreliable". They
   demonstrate it — Mira's nine counts, Dev never having thought of the ruins,
   Amara's sentence stopping.
3. **No character knows more than their SPEC.md entry allows.** Mira may hint;
   she may not reveal that she is Forgotten. Amara may report her dreams; she may
   not name Nirvaan.
4. **Divine speech is not archaic.** Nirvaan is plain, present-tense and
   specific. Nothing in the divine register uses "thee", "thou" or "hark".
5. **Lines end on the concrete.** A stone, a page, a count, a lap of the
   training ground.
6. **Consequences are authored in the data, not in prose.** A line that changes
   the world does it through a `DialogueConsequence`, so the writing and the
   systems cannot drift apart.

## Not yet invented

Deliberately empty, so the next writer knows these are open rather than lost:
the other six temples' inhabitants and inscriptions; the Archivist's voice;
the Forgotten as characters rather than a noun; Avarsha's history before the
erasure; the endings' text (SPEC.md section 72 defines their conditions, not
their words); Ishan's family; the name beneath the stone itself.

---

# OPEN QUESTIONS FOR THE AUTHOR

Listed here rather than decided:

1. **Agniya's boss name** — "Agniya, the First Flame" versus SPEC.md's "The
   Flame Sovereign". See above.
2. **"The war that made them seven"** (MEM_003) — is the erasure a war, and were
   there more than eight gods before it?
3. **Does Nirvaan know his own name?** The first cinematic line says he does not.
4. **Is the Temple Guardian a Forgotten?** "A vow spoken by something built to
   keep one thing safe" reads as a construct, but "not quite yours and not quite
   anyone's" reads as an erased person. The slice does not say.

# Game direction — working notes

An early guide to keep development pointed in the same direction. This captures
the current concept, not a complete specification or a list of implemented features.
Change it as playable experiments teach us what works.

## Core idea

A fast-paced, moderately challenging sidescrolling action-adventure starring a
stick-figure person in a world shaped by a child's drawings and imagination.
Hollow Knight is a reference for movement, combat, and exploration feel; the exact
mechanics remain open. Making a fun game comes first, with story developed as needed.

Simple drawn forms get polished curves, expressive animation, shaders, and effects.
Marker, crayon, or a related drawing treatment is still being explored. The style
should make simple characters and creatures feel intentional and support readable
combat without requiring detailed hand-drawn art.

## Three worlds for now

Time travel is the working connection between the worlds. These are imaginative
versions of eras, with room for fantasy rather than strict historical rules.

| World | Identity | Possible ingredients |
| --- | --- | --- |
| Prehistoric | Wild landscapes and large creatures | Dinosaurs, giant plants, caves, volcanoes |
| Medieval | A kingdom of castles and adventure | Knights, dragons, villages, magic |
| Future | Machines and advanced technology | Robots, factories, mechanical hazards |

Keep the initial scope to these three. Specific creatures, locations, and their
order are provisional; additional eras can be considered later.

## Game structure

1. **Distinct worlds:** a more guided opening establishes each world's identity,
   enemies, and challenges.
2. **A major change:** around the middle, an eruption, explosion, or similar event
   alters the landscape and connects the eras. Bridges, holes, broken structures,
   and new passages open access to more of the map.
3. **An open second part:** the player chooses routes and challenges to gain power
   before the final boss. This combines altered earlier levels, mixed regions,
   and entirely new areas.

The intended payoff is greater route freedom. The exact map layout, central
connection point, and travel mechanism remain open; an airship is not required.

## Mixing and reuse

Teach enemies separately, then combine familiar behaviors into harder encounters.
A velociraptor, a robot, and a robot riding a velociraptor illustrate the idea.
Ordinary enemies remain useful for variety, pacing, and showing player growth.

Reuse locations by changing their routes and encounters: a fallen tower might
become a bridge, or a broken castle floor might reveal prehistoric caves. Preserve
enough familiar geography for players to recognize what changed.

These examples guide content design; they do not require a universal creature
combination system or fully simulated terrain destruction.

## Still open

- Player identity, weapons, movement abilities, and progression rewards.
- How travel between eras works and what causes the major event.
- Final boss, motivation, and amount of story.
- World size, connection layout, and required versus optional challenges.
- Exact drawing material and visual treatment.

## Suggested next playable step

Make one small area with satisfying movement, combat, and a representative enemy.
Then try an altered version with a new route and a mixed encounter. Use that to
test whether both the basic action and revisiting changed places are fun before
committing to a large map or content roster.

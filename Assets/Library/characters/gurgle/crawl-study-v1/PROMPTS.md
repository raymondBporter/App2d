# Gurgle crawl study

Generated with the built-in image generation tool, using `Assets/4gh0secedcag1.webp` as a design reference. Original reference preserved.

Status: animation concept, not runtime-ready. The generated PNG is 1774 × 887 RGB, with a painted checkerboard rather than transparency. A targeted transparency correction also returned an opaque image. Keep this in Library until background extraction and frame registration are finished. Generated face and tentacle details vary between frames; animation needs a consistency pass before shipping.

The preview uses eight manually bounded regions, aligned to a shared baseline. Suggested playback: 8 frames/second. Directional flipping in the preview is only a playback transform. No engine integration has been made.

## Generation prompt

Use case: stylized-concept
Asset type: 2D game monster animation sprite sheet, a first production art pass.
Input image 1 is a character design REFERENCE only. Redraw the creature, not the poster. Ignore and omit all lettering.
Primary request: one coherent 8-frame looping crawl / slither cycle for this white, black and red tentacled cartoon blob monster. Preserve its identity: off-white amorphous body, huge central black open mouth with chunky triangular white teeth and red dangling tongue, small secondary mouth lower left, bulging eye on upper left, two crooked red-tipped horns, small stalk-mounted head/eye on upper right, red tentacles along the base. Simplify small spots and decorative gore for legibility. Playful grotesque ink cartoon, bold uniform black contours, flat white and vivid red fills. No shading, gradients, texture or 3D.
Composition: exactly 4 columns by 2 rows, eight equal square cells, 2048 x 1024 total canvas, each cell 512 x 512. No grid drawn, no labels, no text. Row-major chronological order. Each sprite fully isolated with actual TRANSPARENT background and ample clear margins; white body stays opaque. Each creature approximately 410 px wide and 390 px tall, identical identity, camera, scale and front three-quarter orientation in all eight frames. Feet/tentacles touch the exact same ground baseline at y=460 within each cell, body center at x=256. No overlap between cells and no cropping.
Motion: animates crawling in place toward screen right with a fluid tentacle-driven weight shift. Frames 1-8: neutral contact; reach forward and slight stretch; front tentacle planted and body squashed; drag rear tentacle forward with body rising; opposite contact; opposite tentacle reach with stretch; second squash and pull; recover toward first pose. Meaningful but controlled silhouette changes in every frame. Horns, eye stalk and tongue follow through with small delays. Central face remains recognizable and coherently attached, keep anatomy and number of features consistent. Body bob only about 12 px, body squash/stretch at most 8 percent. No walking legs, no additional characters, no background, no ground puddle or scattered detached specks. This is an evenly registered animation sheet, not a collage of monster redesigns.

## Attempted correction prompt

Use case: background-extraction. Edit target is the attached eight-frame monster sprite sheet. Correct ONLY the background: remove the painted light gray/white checkerboard completely and export actual PNG alpha transparency. Do NOT draw a checkerboard pattern. All empty space outside each creature and the gaps between horns, tentacles and eye stalks must have zero alpha. Keep the creature's white body and white eyes and teeth solid opaque white. Preserve all eight existing poses, black outlines, red accents, layout, and proportions exactly. No new text or features. Deliver as transparent PNG.

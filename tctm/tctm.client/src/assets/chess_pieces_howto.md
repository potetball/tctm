# Chess Pieces SVG Spritesheet — How To

## Overview

The file `chess_pieces_1.svg` is an SVG spritesheet containing all 12 standard chess pieces (6 white, 6 black). Each piece is defined as a `<symbol>` inside `<defs>`, so the SVG itself renders nothing visible — pieces are only drawn when you reference them with `<use>`.

Each symbol has a `viewBox="0 0 45 45"` and scales to any size you set on the outer `<svg>` element.

## Available Symbol IDs

| ID   | Piece         |
|------|---------------|
| `wK` | White King    |
| `wQ` | White Queen   |
| `wB` | White Bishop  |
| `wN` | White Knight  |
| `wR` | White Rook    |
| `wP` | White Pawn    |
| `bK` | Black King    |
| `bQ` | Black Queen   |
| `bB` | Black Bishop  |
| `bN` | Black Knight  |
| `bR` | Black Rook    |
| `bP` | Black Pawn    |

## Usage

### 1. Inline the spritesheet (recommended for Vue / SPA)

Include the SVG once in your page (hidden), then reference pieces anywhere:

```html
<!-- Include the spritesheet (renders nothing visible) -->
<div style="display: none;">
  <!-- Paste or inline the contents of chess_pieces_1.svg here -->
</div>

<!-- Then use pieces by ID -->
<svg width="60" height="60">
  <use href="#wK" />
</svg>

<svg width="45" height="45">
  <use href="#bQ" />
</svg>
```

### 2. Reference as an external file

You can point `<use>` at the external SVG file directly. Note: this approach has cross-origin restrictions and won't inherit CSS from the parent document.

```html
<svg width="60" height="60">
  <use href="/src/assets/chess_pieces_1.svg#wK" />
</svg>
```

### 3. In a Vue component

```vue
<template>
  <svg :width="size" :height="size">
    <use :href="`#${pieceId}`" />
  </svg>
</template>

<script setup>
defineProps({
  pieceId: { type: String, required: true },   // e.g. "wK", "bP"
  size:    { type: [Number, String], default: 45 }
})
</script>
```

### 4. Dynamically mapping piece codes to IDs

The symbol IDs follow the pattern **`{color}{piece}`** where:

- **color**: `w` (white) or `b` (black)
- **piece**: `K` (King), `Q` (Queen), `R` (Rook), `B` (Bishop), `N` (Knight), `P` (Pawn)

This makes it easy to construct IDs programmatically:

```js
const pieceId = `${color}${pieceType}` // e.g. "wK", "bN"
```

## Sizing

The pieces scale cleanly to any size. Just set `width` and `height` on the outer `<svg>`:

```html
<!-- Small (30×30) -->
<svg width="30" height="30"><use href="#wP" /></svg>

<!-- Large (120×120) -->
<svg width="120" height="120"><use href="#bK" /></svg>
```

You can also use CSS to control size:

```css
.chess-piece {
  width: 3rem;
  height: 3rem;
}
```
# 🎨 VISUAL REFERENCE - HOW ARENA SHOULD LOOK

## LAYOUT STRUCTURE (Desktop View)

```
┌─────────────────────────────────────────────────────────────────┐
│                         🎯 DualMind Arena                       │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Enter your prompt...                                    │  │
│  │                                                          │  │
│  │                                            [Battle! →]   │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│  ┌──────────────────────────┐  ┌──────────────────────────┐   │
│  │  Model A                 │  │  Model B                 │   │
│  │  ─────────────────────   │  │  ─────────────────────   │   │
│  │                          │  │                          │   │
│  │  Response text streams   │  │  Response text streams   │   │
│  │  here word by word...▊   │  │  here word by word...▊   │   │
│  │                          │  │                          │   │
│  │                          │  │                          │   │
│  │                          │  │                          │   │
│  │                          │  │                          │   │
│  │  [Vote for Model A]      │  │  [Vote for Model B]      │   │
│  └──────────────────────────┘  └──────────────────────────┘   │
│                                                                 │
│                      [🤝 It's a Tie]                            │
│                                                                 │
│                    Battles completed: 5                         │
└─────────────────────────────────────────────────────────────────┘
```

## AFTER VOTING (Winner Revealed)

```
┌─────────────────────────────────────────────────────────────────┐
│                         🎯 DualMind Arena                       │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  "Write a haiku about coding"                            │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│  ┌──────────────────────────┐  ┌──────────────────────────┐   │
│  │  GPT-4          ✓ WINNER │  │  Claude 3 Opus           │   │
│  │  ─────────────────────   │  │  ─────────────────────   │   │
│  │  🟢 GREEN BORDER         │  │  Regular border          │   │
│  │                          │  │                          │   │
│  │  Code flows like water   │  │  Binary thoughts arise   │   │
│  │  Logic dances on screen  │  │  Functions intertwine    │   │
│  │  Bugs fade into night    │  │  Console speaks the truth│   │
│  │                          │  │                          │   │
│  │  [Voted ✓]               │  │  [Not selected]          │   │
│  └──────────────────────────┘  └──────────────────────────┘   │
│                                                                 │
│                    [⚔️ Next Battle]                             │
│                                                                 │
│                    Battles completed: 6                         │
└─────────────────────────────────────────────────────────────────┘
```

## MOBILE VIEW

```
┌─────────────────────────┐
│   🎯 DualMind Arena     │
│                         │
│  ┌──────────────────┐   │
│  │  Your prompt...  │   │
│  │                  │   │
│  │      [Battle!]   │   │
│  └──────────────────┘   │
│                         │
│  ┌──────────────────┐   │
│  │  Model A         │   │
│  │  ──────────────  │   │
│  │  Response...▊    │   │
│  │                  │   │
│  │  [Vote A]        │   │
│  └──────────────────┘   │
│                         │
│  ┌──────────────────┐   │
│  │  Model B         │   │
│  │  ──────────────  │   │
│  │  Response...▊    │   │
│  │                  │   │
│  │  [Vote B]        │   │
│  └──────────────────┘   │
│                         │
│     [🤝 Tie]            │
└─────────────────────────┘
```

## COLOR SCHEME

### Light Mode
- **Background**: White (#FFFFFF)
- **Cards**: Light gray border (#E5E7EB)
- **Text**: Dark gray (#111827)
- **Primary Button**: Blue (#3B82F6)
- **Winner Border**: Green (#10B981)
- **Cursor**: Blue (#3B82F6)

### Dark Mode
- **Background**: Dark (#0A0A0A)
- **Cards**: Dark gray border (#374151)
- **Text**: Light gray (#F9FAFB)
- **Primary Button**: Blue (#3B82F6)
- **Winner Border**: Green (#10B981)
- **Cursor**: Blue (#3B82F6)

## ANIMATIONS

### Streaming Cursor
```
▊ ← Blinking cursor
Animation: 1s blink infinite
```

### Hover Effects
```
Button hover: Slight scale up (1.02x)
Card hover: Subtle shadow increase
Transition: 200ms ease
```

### Vote Animation
```
Winner card: Smooth green border fade-in
Duration: 300ms
```

## SPACING & SIZING

### Desktop
- **Container max-width**: 1200px
- **Card width**: 50% each (with gap)
- **Gap between cards**: 24px
- **Padding**: 24px all around
- **Button height**: 48px
- **Input height**: 120px

### Mobile
- **Cards**: Full width, stacked vertically
- **Gap between cards**: 16px
- **Padding**: 16px
- **Button height**: 48px
- **Input height**: 100px

## TYPOGRAPHY

- **Title**: 2xl font, bold (DualMind Arena)
- **Model name**: lg font, semibold
- **Response text**: base font, normal
- **Button text**: base font, medium

## KEY FEATURES

1. **Streaming cursor** - Shows AI is thinking
2. **Disabled vote during streaming** - Prevent early votes
3. **Green border winner** - Clear visual winner
4. **Clean spacing** - Not cramped
5. **Responsive** - Works on all screens
6. **Smooth transitions** - No jarring changes

## STATES

### 1. IDLE (Ready for input)
- Empty cards
- Prompt input focused
- Battle button enabled

### 2. LOADING (Generating)
- Battle button disabled
- Both cards show "Generating..."
- No vote buttons visible

### 3. STREAMING (AI responding)
- Text appearing token by token
- Blinking cursor in both cards
- Vote buttons disabled

### 4. COMPLETED (Ready to vote)
- Both responses fully shown
- No cursors
- Vote buttons enabled (blue)
- Tie button visible

### 5. VOTED (Winner revealed)
- Winner card: Green border + checkmark
- Model names revealed
- Vote buttons disabled
- "Next Battle" button appears

## PERFORMANCE TARGETS

- **Frame rate**: 60fps during streaming
- **Token delay**: 20-50ms per token
- **Smooth scrolling**: No jank
- **Memory**: <50MB per session
- **Load time**: <1s initial load

## COMPARISON TO YUP.AI

| Feature | yup.ai | Our Arena | Status |
|---------|--------|-----------|--------|
| Side-by-side | ✓ | ✓ | Match |
| Smooth streaming | ✓ | ✓ | Match |
| Clean UI | ✓ | ✓ | Match |
| Vote buttons | ✓ | ✓ | Match |
| Mobile friendly | ✓ | ✓ | Match |
| Fast load | ✓ | ✓ | Match |
| Model reveal | ✓ | ✓ | Match |

---

## 🎯 GOAL

Make it look and feel EXACTLY like this. Clean, fast, smooth.
No extra features, no clutter. Just perfect execution.


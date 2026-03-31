# ClipVault V1 Roadmap

## Product goal

ClipVault V1 is a polished Windows desktop clipboard manager focused on **text clipboard history**.

The goal is to ship a version that feels reliable, useful, and intentional without overbuilding.

## V1 definition

ClipVault V1 should:

- capture copied text automatically
- keep a searchable clipboard history
- allow pinning important items
- support reusable snippets
- persist data between launches
- minimize to the tray and reopen cleanly
- provide solid error handling and logging
- feel polished enough to use daily

## Current foundation

Already built:

- main shell UI
- sidebar navigation
- top search bar
- mock clipboard cards
- copy / pin / delete actions
- starter settings page
- global exception handling
- file logging
- clipboard retry logic
- guarded UI actions

## V1 scope

### Must-have features

#### 1. Clipboard capture
- monitor clipboard for text changes
- auto-add new copied text to history
- ignore empty text
- avoid duplicate spam
- timestamp every entry
- show newest items first

#### 2. History management
- display clipboard history
- copy an old item again
- pin / unpin items
- delete individual items
- clear all non-pinned history
- search/filter instantly

#### 3. Snippets
- save reusable snippets
- edit snippet title/content
- delete snippets
- copy snippets instantly
- keep snippets separate from normal clipboard history

#### 4. Persistence
- save clipboard history locally
- save pinned state locally
- save snippets locally
- restore all of it on app launch
- save and restore settings

#### 5. App behavior
- start reliably
- handle clipboard access failures gracefully
- log errors locally
- support tray icon behavior
- minimize to tray
- reopen from tray
- optionally launch on Windows startup

#### 6. Settings
- enable/disable clipboard monitoring
- set max history size
- enable/disable launch on startup
- clear history
- show app version
- keep a dark theme for V1

## Recommended V1 quality features

These should be included because they make the app feel complete:

- dedupe logic
- max history trimming
- destructive action confirmations
- polished empty states
- visible success/failure status messages
- graceful handling when clipboard is locked
- real app icon / branding
- version display
- clean publish output

## Out of scope for V1

Do not build these yet:

- image clipboard support
- file clipboard support
- OCR
- cloud sync
- accounts/login
- plugin system
- hotkey customization UI
- multiple themes
- analytics/telemetry
- encryption
- export/import unless time allows
- DevExpress-heavy redesign
- purely cosmetic animation work

## Technical decisions

### Platform
- Windows desktop
- WPF on .NET 10

### Storage
- SQLite for local persistence

### Logging
- local log files in AppData

### Clipboard monitoring
- use proper clipboard change notification logic
- avoid dumb polling if possible

## Data model

### ClipboardItem
Fields:
- Id
- Title
- Content
- Category / Type
- CapturedAt
- IsPinned
- IsSnippet

### Snippet
Fields:
- Id
- Title
- Content
- CreatedAt
- UpdatedAt

### AppSettings
Fields:
- LaunchOnStartup
- ClipboardMonitoringEnabled
- MaxHistoryItems
- MinimizeToTray
- CloseToTray
- Theme

## V1 screens

### History
- search bar
- clipboard history list
- copy action
- pin action
- delete action
- clear history button
- empty state

### Pinned
- pinned items only
- copy action
- unpin action
- delete action

### Snippets
- snippet list
- add snippet
- edit snippet
- delete snippet
- copy snippet

### Settings
- launch on startup
- enable monitoring
- max history size
- clear history
- open logs location
- app version

## Functional requirements

ClipVault V1 should:

- launch quickly
- capture copied text reliably
- avoid saving the same value repeatedly unless it changed
- survive restarts with history intact
- preserve pinned items during trims
- avoid freezing when clipboard is temporarily unavailable
- show helpful status when an operation fails
- log errors without constantly interrupting the user

## Quality bar before release

### Stability
- no normal-use crashes
- clipboard failures handled cleanly
- tray behavior works every time

### UI quality
- consistent spacing
- no dead controls
- no leftover placeholder text
- polished empty screens
- correct window title/icon/versioning

### Persistence
- history saves and reloads
- snippets save and reload
- settings save and reload
- max history limit works correctly

### Release feel
- real branding
- real app icon
- publish build works
- logs go to a predictable path
- version is visible in the app

## Implementation order

### Phase 1: Make it real
- real clipboard monitoring
- auto-create history entries from copied text
- dedupe logic
- max history trimming

### Phase 2: Persistence
- add SQLite
- save/load clipboard history
- save/load snippets
- save/load settings

### Phase 3: App behavior
- tray icon
- minimize to tray
- startup option
- close behavior

### Phase 4: Snippets and settings
- create/edit/delete snippets
- wire settings toggles
- clear history actions
- add destructive action confirmations

### Phase 5: Release prep
- branding/icon
- versioning
- publish profile
- smoke testing
- cleanup pass

## Main risks

The most likely pain points:

- clipboard access being locked by another app
- duplicate entry spam
- tray behavior quirks
- SQLite setup mistakes
- Windows startup registration issues
- keeping filtering responsive while data updates

## V1 test checklist

Before release, test:

- copy normal text
- copy multiline text
- copy long text
- copy the same text twice
- pin an item, restart, verify it stays pinned
- delete an item, restart, verify it stays deleted
- create a snippet, edit it, copy it, restart
- clear history but keep pinned items
- disable monitoring and verify nothing new gets added
- minimize to tray and reopen
- test launch on startup
- close and reopen without errors
- verify log file creation

## Nice-to-have only if time allows

- global hotkey to open ClipVault
- import/export backup
- inline snippet editing improvements
- richer categories
- copied toast messages

## Final V1 statement

ClipVault V1 is a polished Windows text clipboard manager with:

- live text history
- pinned items
- snippets
- search
- persistence
- settings
- tray support
- strong error handling

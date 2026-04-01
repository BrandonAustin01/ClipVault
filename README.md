# ClipVault

ClipVault is a polished desktop clipboard manager for Windows built with WPF.

It keeps a history of copied text, lets you pin important items, create reusable snippets, review logs, and stay out of the way with tray support. The goal is simple: make copied text easier to keep, find, and reuse without turning the app into a mess.

## Features

- **Clipboard history**
  - Automatically captures copied text and stores it locally
  - Prevents duplicate spam from immediately repeated copies
  - Trims old history based on your configured history limit

- **Pinned items**
  - Pin important clipboard entries so they stay easy to find
  - Pinned items are protected from normal history cleanup

- **Snippets**
  - Create reusable text snippets
  - Edit existing snippets
  - Copy snippets back to the clipboard instantly

- **Search and filtering**
  - Search clipboard history, pinned items, and snippets
  - Filter log entries by level in the log viewer

- **Log viewer**
  - View application logs from inside the app
  - Filter by info, warning, and error entries
  - Copy the visible log output or open the logs folder directly

- **Tray support**
  - Minimize to tray
  - Close to tray
  - Restore the app from the tray icon

- **Startup option**
  - Optionally launch ClipVault at Windows startup

- **Local-first storage**
  - Clipboard items and settings are stored locally on the machine
  - Logs are stored locally as well

- **Built-in updating**
  - Packaged releases support update checks through the ClipVault release feed

## Why ClipVault?

A lot of clipboard tools either feel bloated, ugly, or unreliable. ClipVault is meant to feel clean and fast while still covering the stuff that actually matters:

- recent clipboard history
- pinned items
- reusable snippets
- searchable content
- proper logging
- a polished desktop UI

## Screens

ClipVault V1 includes:

- Main window with History, Pinned, Snippets, and Settings sections
- Snippet editor
- Log viewer
- Dark-styled UI with polished controls

## Installation

### Standard install
Download the latest `Setup.exe` from the ClipVault release location and run it like a normal Windows installer.

### Portable / manual files
Some releases may also include packaged assets for update delivery and distribution.

## Usage

### Clipboard history
Once ClipVault is running, copied text is captured automatically when clipboard monitoring is enabled.

### Pinning
Use the **Pin** action on any history item to keep it important and easy to access.

### Snippets
Open the **Snippets** section to create and manage reusable text blocks.

### Settings
From **Settings**, you can manage:

- Launch on startup
- Clipboard monitoring
- Minimize to tray
- Close to tray
- Maximum history size

### Log viewer
Open the log viewer from the app to:

- inspect logs
- search logs
- filter by level
- copy the visible log output
- open the logs folder

## Data Storage

ClipVault stores its data locally in the user's local application data folder.

That includes:

- the SQLite database
- settings
- log files

## Update Flow

ClipVault supports packaged application updates through its release feed.

Typical release flow:

1. Publish the app
2. Pack the release
3. Upload the generated release assets
4. Install from the packaged setup
5. Use in-app update checking for future versions

## Project Status

**Version 1.0.0** is the first polished release candidate-level version of ClipVault.

V1 includes the completed core experience:

- main window UI polish
- snippet editor polish
- log viewer
- dark ComboBox fix in the log viewer
- packaged updater flow

## Tech Stack

- C#
- WPF
- SQLite
- Velopack for packaged updates

## Notes

ClipVault is currently focused on **text clipboard workflows**.

## License

[MIT License](https://github.com/BrandonAustin01/ClipVault?tab=MIT-1-ov-file)

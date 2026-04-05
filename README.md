<p align="center">
  <img width="124" height="124" alt="ClipVaultLogo" src="https://github.com/user-attachments/assets/bd511f8c-3aa4-40a1-99c1-c88dcab40c74" />
</p>

<h1 align="center">ClipVault</h1>

<p align="center">
  A polished clipboard manager for Windows built with WPF.
</p>

ClipVault helps you keep, organize, and reuse copied text without the clutter. It stores clipboard history locally, lets you pin important entries, create reusable snippets, review logs, and stay out of the way with tray support.

## Features

### Clipboard history
- Automatically captures copied text
- Stores clipboard history locally on your machine
- Prevents duplicate spam from immediately repeated copies
- Trims older entries based on your configured history limit

### Pinned items
- Pin important clipboard entries for quick access
- Protect pinned items from normal history cleanup

### Snippets
- Create reusable text snippets
- Edit existing snippets
- Copy snippets back to the clipboard instantly

### Search and filtering
- Search clipboard history, pinned items, and snippets
- Filter log entries by level in the log viewer

### Log viewer
- View application logs from inside the app
- Filter by info, warning, and error entries
- Copy visible log output
- Open the logs folder directly

### Tray support
- Minimize to tray
- Close to tray
- Restore the app from the tray icon

### Startup option
- Optionally launch ClipVault at Windows startup

### Local-first storage
- Clipboard data is stored locally
- Settings are stored locally
- Logs are stored locally

### Built-in updating
- Packaged releases support update checks through the ClipVault release feed

## Why ClipVault?

A lot of clipboard tools feel bloated, outdated, or unreliable.

ClipVault is designed to stay clean, fast, and focused on the features that matter most:

- Clipboard history
- Pinned items
- Reusable snippets
- Searchable content
- Built-in logging
- A polished desktop UI

It is built for people who want a better clipboard workflow without turning a simple utility into a mess.

## Included in v1

ClipVault v1 includes:

- Main window with **History**, **Pinned**, **Snippets**, and **Settings**
- Snippet editor
- Built-in log viewer
- Dark-styled UI with polished controls
- Packaged updater support

## Installation

### Standard install
Download the latest `Setup.exe` from the latest release and run it like a normal Windows installer.

### Portable / release assets
Some releases may also include packaged assets used for update delivery and distribution.

## Usage

### Clipboard history
Once ClipVault is running, copied text is captured automatically when clipboard monitoring is enabled.

### Pinning
Use the **Pin** action on any history item to keep it available and easy to find.

### Snippets
Open the **Snippets** section to create, edit, and reuse saved text blocks.

### Settings
From **Settings**, you can manage:

- Launch on startup
- Clipboard monitoring
- Minimize to tray
- Close to tray
- Maximum history size

### Log viewer
Use the built-in log viewer to:

- Inspect logs
- Search log content
- Filter by level
- Copy visible log output
- Open the logs folder

## Data Storage

ClipVault stores its data locally in the user's local application data folder.

This includes:

- SQLite database
- Settings
- Log files

## Update Flow

ClipVault supports packaged application updates through its release feed.

Typical release flow:

1. Publish the app
2. Pack the release
3. Upload the generated release assets
4. Install using the packaged setup
5. Use in-app update checking for future versions

## Project Status

**Version 1.x** represents the first polished foundation of ClipVault.

Current releases include the core experience:

- Main window UI polish
- Snippet editor polish
- Log viewer
- Theme and shared UI improvements
- Packaged updater flow

## Tech Stack

- C#
- WPF
- SQLite
- Velopack

## Notes

ClipVault is currently focused on **text clipboard workflows**.

## License

[MIT License](https://github.com/BrandonAustin01/ClipVault?tab=MIT-1-ov-file)

# McfEditor

**McfEditor** is a WPF desktop tool for extracting, previewing, replacing and rebuilding **MIB2 `.mcf` image archives**.

This startup package was prepared to mirror the structure and visual style of your **GcaEditor** project as closely as possible, so you can drop it next to your current repo, open it in **Visual Studio 2022**, and keep iterating from a familiar base.

## Current startup scope

This first package already includes:

- the same dark theme base as GcaEditor
- a similar solution layout and release workflow
- MCF extraction
- image list + search
- preview panel
- replace / restore workflow
- rebuild to a new `.mcf`
- settings window
- themed message box
- release script and release notes template

## Project structure

```text
McfEditor/
├── McfEditor.sln
├── README.md
├── RELEASE_NOTES.md
├── release.ps1
└── McfEditor/
    ├── Assets/
    ├── Data/
    ├── IO/
    ├── MainWindow/
    ├── Models/
    ├── Settings/
    ├── Themes/
    ├── UI/
    ├── ViewModels/
    └── Views/
```

## Requirements

- Visual Studio 2022
- .NET 8 SDK

## First run

1. Open `McfEditor.sln` in Visual Studio 2022.
2. Restore NuGet packages if Visual Studio asks.
3. Build and run the project.
4. Open an `.mcf` file.
5. The app extracts images into a temporary working folder.
6. Replace one or more `img_<index>.png` files through the UI.
7. Rebuild a new `.mcf`.

## Notes

McfEditor now uses a fully native C# backend for MCF extraction, imageidmap parsing and rebuild.
No external Python installation is required.
- This package is meant as a **clean starting point**, not a finished editor.
- I could not compile-test the project in this environment, so the first Visual Studio pass should be treated as the real validation step.

## Suggested roadmap

### v0.1.0
- open / extract MCF
- preview images
- replace and restore PNGs
- rebuild MCF
- validate startup workflow

### v0.1.1
- dimension / mode validation before replace
- drag & drop image replacement
- remember more workspace state
- better rebuild logging

### v0.2.0
- native C# parsing for headers / manifest generation
- stronger validation and diagnostics
- diff / compare before-after preview

## Disclaimer

This project is intended for research and development purposes only.

It is not affiliated with or endorsed by Volkswagen AG.

Use at your own risk when modifying files used in vehicle systems.

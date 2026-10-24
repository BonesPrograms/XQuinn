# Documentation

## Current guides

| Guide | Contents |
| --- | --- |
| [Runtime Navigator](Runtime.Navigator.md) | Logger setup, controllers, references, queries, invocation, and assignment |
| [TypeRegister](TypeRegister.md) | Public declaration, cache operations, generic keys, and naming restrictions |
| [Public API notes](PublicAPI.txt) | Public versus internal implementation types |
| [Library overview](XQuinn.txt) | Entry points and utility categories |

The old [Navigator text-document location](Runtime.Navigator%20Public%20API%20Doc.txt)
now points to the formatted guide. The other `.txt` files in this directory are
development notes, ideas, or historical platform notes, not a current API contract.

## Verified examples

| Screenshot | Searchable transcript | What it demonstrates |
| --- | --- | --- |
| [Reference lifecycle](Screenshots/navigator-references.png) | [Text](Screenshots/navigator-references.txt) | Assigned references, loaded reference identity, enumeration, and `?refs` |
| [Member queries](Screenshots/navigator-queries.png) | [Text](Screenshots/navigator-queries.txt) | `$`, query filters, actual constructor keys, and overload signatures |
| [TypeRegister](Screenshots/type-register.png) | [Text](Screenshots/type-register.txt) | Non-partial declaration, case-insensitive lookup, generic definitions, and cache results |

## Screenshot rules

No earlier screenshot assets or screenshot-specific rules were present in this
checkout. These rules make the new captures reproducible:

1. Build and execute the current source before capturing anything. Do not invent
   returned values or paste output from an older binary.
2. Render the captured output in a browser. These are screenshots of a runtime
   transcript page, **not** photographs of the interactive console or a game UI.
3. Keep the full command, returned result, loaded type, object type, and
   `Reference:` lines visible whenever the runtime emits them. For references,
   include `?refs` as well as loading the saved reference.
4. Include a readable source caption and the source revision in each image.
   Keep a plain-text transcript alongside every PNG for copying and searching.
5. Omit only the wall-clock timestamp that Navigator writes before each command.
   Do not remove exception output, crop away relevant state, or alter return text.
6. Keep source links relative to the guide so they work in a local Markdown
   preview and in a repository browser. Use descriptive image alternative text.
7. Regenerate all three captures with
   [the documentation script](Tools/Update-Documentation.ps1). The script checks
   the examples, image dimensions, and documentation links before succeeding.

Source of truth: [Navigator](../Library/Runtime/Navigator.cs),
[NavigatorCore](../Library/Runtime/NavigatorCore.cs), and
[TypeRegister](../Library/Reflection/TypeRegister.cs).
# Greeter

Greeter is a focused Dalamud plugin for FFXIV venue staff. It automatically welcomes newly arrived guests inside one configured housing interior while avoiding repeat greetings during the same game session.

Version 1.0 is intentionally limited to greeting. It has no server, no accounts, no staff synchronization, and no other venue-management features.

## What it does

- Activates only inside the exact house or apartment you configure. Seeing someone in Limsa, another house, or anywhere else does not count.
- Detects a guest as an arrival only after they remain visible for the configured confirmation time.
- Greets an ordinary guest once per running FFXIV session.
- Keeps the session list across a Dalamud plugin reload, but clears it when FFXIV closes, you log out, or you change characters.
- Supports `Always Greet` and `Never Greet` lists using full character name plus Home World.
- Lets you add player rules from a player context menu or by typing the name and world.
- Queues simultaneous arrivals, applies randomized spacing, rechecks presence before sending, and expires stale greetings.
- Supports multiple randomized greeting templates and `/say`, `/yell`, or `/shout` output.

`Never Greet` wins if a character somehow appears in both lists. Adding a character to either list through Greeter automatically removes them from the other.

## First-time setup

1. Install and enable Greeter in Dalamud.
2. Enter the interior of the venue house.
3. Run `/greeter`.
4. On **Venue & Automation**, click **Set Current House as Venue**.
5. Enter the venue name and edit the greeting templates.
6. Leave automatic greetings enabled. Greeter now operates without a manual action for each guest.

By default, guests already present during Greeter's initial room scan are treated as a baseline and are not greeted. This prevents a plugin reload or venue setup from greeting the entire room at once. You can change that behavior in the settings.

## Greeting variables

| Variable | Result |
| --- | --- |
| `{first}` | Character's first name |
| `{last}` | Character's last name |
| `{fullname}` | Full character name |
| `{world}` | Home World |
| `{venue}` | Configured venue name |

Example: `Welcome to {venue}, {first}!`

## Commands

| Command | Action |
| --- | --- |
| `/greeter` | Open settings |
| `/greeter on` | Enable automatic greetings |
| `/greeter off` | Disable automatic greetings |
| `/greeter status` | Print current state and counts |
| `/greeter setvenue` | Set the housing interior you are currently inside |
| `/greeter unsetvenue` | Clear the configured venue |
| `/greeter test` | Send one real test greeting while inside the configured venue |
| `/greeter clear` | Clear the greeted-this-session list |
| `/greeter debug on` | Show diagnostic activity in your chat |
| `/greeter debug off` | Hide diagnostic activity |
| `/greeter help` | Print the command summary |

## How arrival handling works

Greeter distinguishes four different states:

1. A player seen outside the configured venue is ignored completely.
2. A player seen inside the configured venue must remain visible for the arrival confirmation period.
3. A normal guest is queued only if they have not already been greeted in this FFXIV session.
4. An Always Greet guest may be greeted again after a genuine departure and re-entry. They are not repeatedly greeted while standing in the room.

A player must remain absent for the departure confirmation period before Greeter considers them gone. This protects against brief object-table culling in crowded rooms.

## Safety notice

Automatic greetings send real game chat messages without a separate confirmation click. No third-party automation can be guaranteed safe from Square Enix enforcement. Review your templates and pacing settings, avoid spam, and use this feature at your own risk.

## Building

Requirements:

- Windows or a compatible .NET build environment
- .NET 10 SDK
- A current Dalamud development installation

Build the release package from the repository root:

```powershell
dotnet build Greeter.slnx --configuration Release
```

The Dalamud SDK places the packaged plugin under `Greeter/bin/x64/Release/Greeter/` and produces `latest.zip` for a custom-repository release.

## Custom repository publishing

1. Build the Release configuration.
2. Create the GitHub release tag `V1.0.0.0`.
3. Upload the generated `latest.zip` to that release.
4. Ensure `repo.json` is present on the `main` branch.
5. Add this URL to Dalamud's custom plugin repositories:

```text
https://raw.githubusercontent.com/anomalousgamer/greeter/main/repo.json
```

Greeter targets Dalamud API level 15.

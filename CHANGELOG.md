# Changelog

## 1.1.0.0 — Hotfix

* Fixed deleted default greetings returning when the plugin was reloaded.
* Ensured saved greeting collections replace first-run defaults during configuration loading.
* Added a Home World dropdown populated from all public worlds in FFXIV's current game data.
* Added `/tell` as a greeting channel, addressed by guest name and Home World.
* Added a Clear Activity Log button.
* Added a defensive configuration save when Greeter unloads.
* Preserved existing venue settings, custom greetings, guest rules, and timing values.

## 1.0.0.0

* Added exact indoor-house venue matching.
* Added automatic arrival detection with confirmation and object-culling protection.
* Added one-greeting-per-game-session duplicate protection.
* Added Always Greet and Never Greet rules by character name and Home World.
* Added player context-menu rule actions.
* Added customizable greeting templates and variables.
* Added randomized queue pacing, presence rechecks, retry handling, and stale-message expiry.
* Added `/say`, `/yell`, and `/shout` channel selection.
* Added settings, status, testing, diagnostics, and activity history.
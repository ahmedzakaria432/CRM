# communication-channels — plan overview

> **MVP re-scope (2026-09-29): this whole feature is deferred post-MVP.** Do not plan its stories now. See `.squad/BACKLOG.md`, "MVP re-scope".

Entry point for the **Communication Channels** feature. This is **feature 5 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

**Backend modules:** creates `Communication` (22; also 23–27). Inbound messages reach `Tickets` as integration events. See the module catalogue in `CLAUDE.md`.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 22 | [`channel-abstraction-and-inbound-message-pipeline`](../../stories/communication-channels/channel-abstraction-and-inbound-message-pipeline/intake.md) | Channel abstraction and inbound message pipeline | _not planned yet_ | CRM-11, CRM-15, CRM-16, CRM-18 |
| 23 | [`email-channel-inbound-and-outbound`](../../stories/communication-channels/email-channel-inbound-and-outbound/intake.md) | Email channel inbound and outbound | _not planned yet_ | CRM-22 |
| 24 | [`whatsapp-business-channel`](../../stories/communication-channels/whatsapp-business-channel/intake.md) | WhatsApp Business channel | _not planned yet_ | CRM-22 |
| 25 | [`sms-channel`](../../stories/communication-channels/sms-channel/intake.md) | SMS channel | _not planned yet_ | CRM-22 |
| 26 | [`live-chat-with-realtime-agent-console`](../../stories/communication-channels/live-chat-with-realtime-agent-console/intake.md) | Live chat with realtime agent console | _not planned yet_ | CRM-22, CRM-15, CRM-18, CRM-12 |
| 27 | [`web-form-intake-and-public-submission-endpoint`](../../stories/communication-channels/web-form-intake-and-public-submission-endpoint/intake.md) | Web form intake and public submission endpoint | _not planned yet_ | CRM-22, CRM-16, CRM-12 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._

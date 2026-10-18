# FloodLink AI — User Guide

For field volunteers, relief coordinators, depot managers, and administrators.

Version: 18 October 2026. This guide describes the screens implemented in the repository. Your organisation supplies the portal address, mobile app, and staff credentials; availability depends on its deployment.

## 1. What FloodLink does

FloodLink connects field reports about shelter needs with relief supplies. Volunteers submit reports, coordinators start a planning workflow, and the application prioritises needs, matches depot stock, calculates routes and estimated travel times, and checks the proposed plan. A coordinator then approves, rejects, or requests changes. Volunteers can track the result and confirm delivery.

**A submitted report does not automatically dispatch supplies. A coordinator must start the workflow and approve the resulting plan.**

## 2. Choose the right application

| Role | Application | Main tasks |
| --- | --- | --- |
| Volunteer | Mobile field app | Register, report shelter needs, view own reports and supply status, view routes, confirm delivery |
| Coordinator | Web relief coordination portal | Review reports and shelters, start and monitor workflows, review safety checks, decide on plans, inspect dispatch history and analytics |
| Depot manager | Web portal and mobile field app | Check stock, record deliveries into a depot, hold/release stock on web, manage depots on web, review allocations and mobile low-stock alerts |
| Administrator | Web portal; mobile access also available | Access operational screens and create Coordinator or DepotManager accounts |

Navigation follows your role. Volunteers use the mobile app; coordinators use the web portal. Depot managers start on Inventory on web and Stock Check-in on mobile.

## 3. Sign in and get ready

### Volunteers

1. Open the mobile app and choose the account-creation option on the sign-in screen.
2. Enter your name, email address, phone number, and password.
3. Use a password of at least eight characters. The phone field accepts 7–20 characters consisting of digits/spaces, with an optional leading `+`.
4. Choose **Create account** and sign in when prompted.

Self-registration creates a Volunteer account. For staff access, contact an administrator.

### Staff

1. Open the web portal address supplied by your organisation, or the mobile app if you are a depot manager.
2. Enter the email and password supplied for your account.
3. Check the displayed name and role before working.

Sessions expire after approximately eight hours. If the application returns you to sign-in, authenticate again. Use **Sign out** when finished, particularly on shared devices. Contact your administrator for account or password assistance; the current screens do not provide a password-reset flow.

### Connectivity and permissions

Use an internet connection for submission, stock changes, and status updates. Allow camera access to take evidence photos and location access to capture GPS coordinates. Mobile notifications may require a separate permission.

The current report form has no persistent offline queue or automatic later synchronisation. Keep a separate record of observations when disconnected, and submit after reconnecting. Do not assume an unsent form survives closing the app.

## 4. Volunteers: submit a field report

1. On **FloodLink Field Reports**, tap **New Report**.
2. Select the correct shelter. The dropdown shows its name and occupancy/capacity; check the selection rather than accepting the first shelter automatically.
3. Select **Need Type**: Water, Food, Medical, Shelter-Repair, or Other.
4. Enter **Quantity Required** as a whole number greater than zero. Explain the unit in your notes if necessary.
5. Add **Additional Notes & Justification**, up to 500 characters. Describe the observed need, urgency, and relevant constraints.
6. Optionally capture a photo using the form's camera option.
7. Optionally open the GPS option. Wait for a location fix, check the map, and adjust the pin if needed before confirming coordinates. If GPS fails, verify the manual pin; the initial map position may be Colombo rather than your location.
8. Review the shelter, quantity, evidence, and location, then submit.
9. Wait for **Field report submitted successfully!** and check that the report appears in your list.

Example notes: “Water needed for 50 residents for today. Quantity is 100 litres. Access is through the eastern entrance.”

Report photos must be JPEG, PNG, or WebP and no larger than 10 MB. If shelters cannot be loaded or the correct shelter is missing, retry or contact the coordinator rather than submitting against another shelter.

If submission fails, read the message and retry after correcting the problem. If the connection drops during submission, refresh your reports first to check whether the report was saved before submitting again.

## 5. Volunteers: follow a request and confirm delivery

### Track progress

1. Open your reports on the home screen or use **My Submitted Reports** in the top bar.
2. Refresh the list if needed.
3. Tap the supply-status link on a report once it has a workflow. Before then, it says **Waiting for a coordinator to plan supplies**.
4. On **Supply request status**, review the stage, coordinator note, and route/ETA when available.
5. Use **View route on map** to inspect available route legs.

The status screen checks for updates about every five seconds while planning is active and stops polling when the run settles. Status notifications are generated while the screen detects changes; they are not a guaranteed background push service.

### Confirm delivery

1. After an approved dispatch actually arrives, open its supply-status screen and tap **Confirm delivery**.
2. Add delivery notes, such as receipt details or discrepancies.
3. Optionally choose **Take photo** or **Choose photo**. You can remove an attachment before submission.
4. Tap **Confirm delivery** and wait for the success screen, then tap **Done**.

You can also open **Dispatch & Delivery** from the report list to look up a workflow or enter its run ID for delivery confirmation. Use the workflow ID, rather than a report number.

Volunteers can access runs linked to their own reports. Delivery confirmation requires an Approved run and resolves the linked reports. The workflow can continue to show **Approved** after delivery; consult the delivery indication and audit record as well.

## 6. Coordinators: review reports and start a workflow

### Review shelters and needs

Open **Shelters** to search shelters and inspect occupancy, location, and shelter details. Select a shelter to view its available details and reports. The map presentation depends on deployment configuration.

Open **Reports** to review submitted needs, notes, urgency, photos, and GPS evidence. Search by need, notes, shelter, or report ID; use the available filters, sorting, and page controls to narrow the queue.

### Start planning

1. In **Reports**, select the reports to include. Only **New** or **Triaged** reports are selectable for a new workflow.
2. Check the selection count. Selections remain selected when moving between result pages.
3. Enter the workflow objective, for example, “Water and medical supplies for Kaduwela shelters.”
4. Choose **Start workflow**.
5. Review the workflow trace that opens.

The application creates a plan, then runs Triage → Matching → Routing → Validating. A successful plan waits at **PendingApproval** for a coordinator decision. Some requests may remain partly or wholly uncovered when stock cannot meet the need; inspect the allocation and uncovered-needs information.

### Monitor and inspect a workflow

Open **Workflows** to search, filter by stage, and page through runs. The monitor refreshes approximately every five seconds. Open a run to see:

- The objective and plan.
- Execution timeline and recorded agent/tool activity.
- Triage scores and explanations.
- Allocations from depots to shelters.
- Route legs, distances, estimated travel times, and map.
- Safety-check results.
- Coordinator decisions, decision-maker names, and audit events.

An active trace refreshes approximately every three seconds. **Triage** provides a separate view of prioritisation results.

Some planning steps use AI; rules or a greedy stock-matching method can provide fallbacks when the AI service is unavailable. The trace identifies fallback use. **Explain these results** in the safety section gives a readable explanation; **Regenerate** requests another explanation. Explanations do not change the actual safety pass/fail results.

## 7. Coordinators: approve, reject, or request changes

1. Open **Approvals** and expand the pending run.
2. Review its report count, allocation quantities, shelters, route/ETA, uncovered needs, and safety checks.
3. Use **View trace** for the full supporting details.
4. Choose a decision, enter notes as appropriate, and select **Submit decision**.

| Decision | Notes | Result |
| --- | --- | --- |
| Approve | Optional | Authorises dispatch and commits the reserved stock |
| Reject | Required reason | Rejects the plan and releases its stock reservation |
| Request revision | Required instructions | Releases the existing reservation and sends the workflow back for re-planning |

Write actionable revision instructions, such as “Prioritise medical supplies for Shelter A and review the remaining water allocation.” Review the revised plan when it returns for approval.

If the application refuses a decision because stock or state changed, refresh the run and follow the error message. A safety failure requires investigation; a readable AI explanation does not override the checks.

Open **Dispatches** to filter decision history and expand audit details. Open **Analytics** for summary measures, approval timing, and decisions per shelter.

## 8. Depot managers: manage stock and depots

### Understand stock quantities

| Term | Meaning |
| --- | --- |
| Available/on-hand quantity | Recorded stock before subtracting reservations |
| Reserved | Stock held for plans or manual holds |
| Free stock | Available quantity minus reserved quantity |
| Low stock | Free stock at or below the item's reorder threshold |

### Web: receive, hold, or release stock

1. Open **Inventory**.
2. Search by item or depot, use **Low stock only**, or change the sort order.
3. Find the correct stock line and choose **Check in**, **Hold**, or **Release** where available.
4. Enter a positive quantity in the displayed unit and confirm the action.
5. Check that the refreshed quantities match the intended change.

**Check in** adds newly received supplies. **Hold** reserves free stock. **Release** reduces reserved stock. Coordinate manual releases with the relief coordinator so they do not interfere with pending plans. The server refuses quantities exceeding the applicable free or reserved stock; use its message to resolve the issue.

Coordinators can inspect inventory but cannot use depot-manager stock-edit actions.

### Mobile: record incoming stock

1. Sign in as a depot manager to open **Stock Check-in**.
2. Select the depot and an existing stock item.
3. Enter the quantity physically received.
4. Tap **Record check-in** and wait for **Stock check-in recorded.**
5. Open the low-stock screen from the check-in screen to review items needing replenishment.

### Web: manage depots

1. Open **Depots** and search for a depot or manager.
2. Choose **Add depot** or **Edit**.
3. Enter the name and accurate latitude/longitude, and select the responsible manager if known.
4. Choose **Save depot** and verify the saved row.

Latitude must be between −90 and 90, longitude between −180 and 180, and `(0, 0)` is rejected. Coordinators have read-only access to this page.

### Review allocations

Open **Allocations** to see the sending depot, receiving shelter, item, quantity, and workflow. Filter by status:

- **Proposed**: matched, awaiting validation.
- **Reserved**: held while awaiting approval.
- **Committed**: approved and deducted from stock.
- **Released**: reservation returned following rejection or revision.

Coordinators and administrators can use **Open trace** to inspect the associated workflow. Depot managers see the workflow identifier.

## 9. Administrators: create staff accounts

1. Sign in to the web portal as an administrator and open **Users**.
2. In **Create staff account**, enter the person's name, email, phone, and temporary password of at least eight characters.
3. Select **Coordinator** or **Depot manager**.
4. Choose **Create account** and check the success message and account list.
5. Provide credentials to the intended staff member through your organisation's approved channel.

An email address can belong to only one account. Volunteers register in the mobile app. The current Users screen lists accounts and creates staff; it does not provide account deletion, role editing, or password changes.

## 10. Status reference

Report status and workflow state describe different parts of the process.

| Report status | Meaning |
| --- | --- |
| New | Submitted for review |
| Triaged | Prioritisation has been recorded |
| InPlan | Included in a response plan |
| Resolved | Delivery has been confirmed for the linked workflow |

| Workflow state | Meaning / next action |
| --- | --- |
| Triage | Prioritising reports |
| Matching | Assigning available stock |
| Routing | Calculating depot-to-shelter routes |
| Validating | Checking the plan against safety rules |
| PendingApproval | Coordinator review required |
| RevisionRequested | Re-planning using coordinator instructions |
| Approved | Dispatch authorised; delivery can be confirmed when received |
| Rejected | Plan rejected; read the coordinator's reason |
| Failed | Planning stopped; coordinator should inspect the trace |

For a Failed run, coordinators can use the **Failed runs** tab in Workflows or **Retry stage** in the trace if retries remain. Investigate the error first. The retry allowance is limited and displayed by the application; the default limit is three.

## 11. Troubleshooting

| Problem | What to do |
| --- | --- |
| Cannot sign in | Check email/password and the correct app for your role. Ask an administrator about missing or locked accounts. |
| Too many login attempts | Wait before trying again; login is rate limited. |
| Returned to sign-in | Sign in again; the session may have expired. |
| Missing menu or access denied | Check the displayed role. Ask an administrator if the account role is incorrect. |
| Cannot load reports, shelters, or stock | Check connectivity, use Refresh/Retry, and contact your support contact if it persists. |
| GPS unavailable | Enable device location and app permission, retry GPS, or carefully position the manual map pin. |
| Photo rejected | Use JPEG, PNG, or WebP within 10 MB. |
| Report has no workflow link | It is waiting for a coordinator to start a workflow. |
| Map is blank or simplified | Check connectivity; maps depend on configured services. Use the available route text and coordinates and report the issue. |
| Workflow Failed | Coordinator: inspect the failed stage and trace, resolve the reported cause, and retry if allowed. |
| Approval or stock change refused | Refresh, read the refusal message, and recheck quantities and workflow state. |
| Cannot confirm delivery | Confirm that the run is Approved and linked to your own report, and check whether delivery was already recorded. |

When requesting help, provide the report ID or workflow ID, screen name, approximate time, and error text. Do not share your password. The portal URL and support contact must be supplied by the organisation running FloodLink.

## 12. Quick end-to-end checklist

1. **Volunteer:** submit the need and verify it appears in your reports.
2. **Coordinator:** select eligible reports and start a workflow.
3. **Coordinator:** review allocations, routes, uncovered needs, and safety checks.
4. **Coordinator:** approve, reject with a reason, or request an actionable revision.
5. **Volunteer:** check the decision and ETA; confirm delivery after actual receipt.
6. **Depot manager:** keep stock receipts and depot information accurate.

## Guide scope

This document was checked against the current web/mobile screens, role routing, report provider, and backend workflow/stock services. It is a source-based guide, not evidence of a completed live deployment or a device walkthrough. Backend endpoints without corresponding user controls are not presented as screen features.

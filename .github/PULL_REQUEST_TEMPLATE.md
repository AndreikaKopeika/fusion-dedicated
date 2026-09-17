## What does this change?

<!-- One or two sentences: the problem and the approach. -->

## Why?

<!-- Link the issue, or explain the motivation if there is no issue. -->

## Checklist

- [ ] `dotnet test` passes locally (CI runs it too)
- [ ] New behaviour is covered by a test, or is not testable without Steam
- [ ] The wire format (`Protocol/`) is unchanged, or the change mirrors LabFusion exactly
- [ ] Old `server.json` files still load
- [ ] The README is updated if operators can observe a difference
- [ ] Security-relevant (permissions, bans, panel) changes are called out here

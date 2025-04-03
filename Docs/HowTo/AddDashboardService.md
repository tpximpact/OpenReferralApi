# Add a feed to the dashboard

Below is a step by step description of how a new feed can be added to the dashboard.

- User [registers their feed for the dashboard](https://openreferraluk.org/developers/register)
- API saves details to the database marking the service as `active: false`
- API raises an issue on the [mhclg-oruk/issues](https://github.com/tpximpact/mhclg-oruk/issues/) page
    - Admin(s) will be assigned to the issue and it will be labelled as `DASHBOARD` & `submission`
    - Who is assigned and what labels are applied are configurable settings that will be updated during deployment and are read from variables in the Github settings
- Admin review the submission details and make decision on adding service to the dashboard
- Admin & user(s) can add comments to the issue in order to ask questions or add relevant context as needed
- If Admin approve submission they mark the service as `active: true` in the database
- Approved or not the Admin should update & close the issue with a comment
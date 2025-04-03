# Dashboard

The dashboard returns data on all the services marked as active in the database as well as definitions describing the type of data and how it can be displayed. 

The aim of returning the data with descriptions for what it is and how to display it is to allow the website to be flexible and the data it displays be driven by the response it receives.

## Dashboard testing

All active services stored in the database are tested by the validator at a regular interval. If these tests should run and at what interval are configurable settings that will be updated during deployment and are read from variables in the Github settings.

## Registering for the dashboard

Users can [register their feed for the dashboard](https://openreferraluk.org/developers/register) using the website. The details submitted are saved to the database and an issue is raised on the [website's Github repo](https://github.com/tpximpact/mhclg-oruk/issues/). Full details on how to add a new service are detailed in the [Add a feed to the dashboard](./HowTo/AddDashboardService.md) docs page.


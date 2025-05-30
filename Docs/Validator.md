# Validator

The purpose of the validator is to assess an API feed against one of the ORUK standards and provide feedback to the user if there are any areas of their API that do not meet the standard.

## How it works

- The validator accepts a url and a profile
    - The url should be the base url of an API service that will be tested against an ORUK schema
    - The profile field is optional. If no profile is provided the validator will judge which test profile to use

### How the test profile is selected

- If the profile parameter is provided
    - Match it to the expected values and use that test profile
    - If it cannot be matched to an expected value it will default to `HSDS-UK-3.0`
    - Expected values
        - `HSDS-UK-3.0` 
        - `HSDS-UK-1.0`
- If the profile parameter is not provided, check the response from the base path of the service
    - If it does not return an ok response or the response cannot be parsed the validator will use `HSDS-UK-1.0`
    - If the response is ok and can be parsed it will try to read the `version` field and follow the same logic as if the profile parameter was provided

### What's tested

- The response from each endpoint is validated against a schema using [Newtonsoft.Json.Schema](https://www.newtonsoft.com/jsonschema/help/html/Introduction.htm)
- If a test is marked for pagination testing then multiple requests are made to validate it works correctly 


## Test profiles

- A test profile exists for each schema 
- A test is described for each endpoint and they are collected together within test groups
- Further details and an example can be found in the [Test Profiles](TestProfiles.md) doc file

#### Test groups

- A test group contains a collection of tests 
- It should have a name and description
- It describes 
    - If the test group is required to pass validation for the overall API service to pass validation
    - The severity level issues should be raised as `error` or `warning`

#### Tests

- A test contains the details of an endpoint
- It should have a name and description
- It describes
    - The endpoint to test
    - The schema that the response should be validated against
    - If the endpoint needs to be tested for pagination
    - If an id is needed for the request and the endpoint it can be retrieved from

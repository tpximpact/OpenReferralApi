# How to add a new schema

This should act as a guide for adding additional schema that an API service can be tested against.

## Schema files

- Add a new folder for the schema in the [OpenReferralApi/Schemas](../../OpenReferralApi/Schemas/) folder 
- For each endpoint add a file containing the response schema
- These response schema files will be referred to by the test profile

## Test profile

- A new test profile needs to be created for the new schema
- More information about test profiles are detailed in [the Validator docs page](../Validator.md) 
- Examples of what a test profile should like can be found in the [OpenReferralApi/TestProfiles](../../OpenReferralApi/TestProfiles/) folder

## Code changes

- A change will need to be made to add the new profile in the [ValidatorService](../../OpenReferralApi/Services/ValidatorService.cs) to the `SelectTestSchema` method
- The aim is to remove any need for code changes when adding new schemas but that has yet to be realised
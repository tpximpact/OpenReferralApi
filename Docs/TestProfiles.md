# Test Profiles

- Test profiles are used to describe 
    - What needs to be tested
    - What tests should be run
    - The details needed to run the tests
    - How the results should be returned by the validator
- An example test profile is included at the end of this page

## Test Groups

- Used to group together test cases
- `required` - bool
    - IF `true` then all of the test group's tests must pass validation for the profile to pass
    - IF `false` then the profile may pass validation despite tests within this test group failing
- `messageLevel` - string
    - This is how failing tests within the test group should be described
    - The ORUK validator website supports `error` & `warning`

## Tests

- Each test details an endpoint, the schema used for validating it, and the tests to be run on it
- Schema validation is run against the response data for all endpoints
- `endpoint` - string
    - The endpoint that the `GET` request will be made to
    - The API base url is provided in the validation request so only the endpoint is needed
    - All requests in the standard are `GET` requests
- `schema` - string
    - The relative file path of the file containing the schema that the response data should be validated against
- `pagination` - bool
    - IF `true` then pagination tests will also be run against the endpoint
- `useIdFrom`
    - For endpoints that require an `id` parameter in the route
    - Should contain the endpoint that will return the ids needed for this request
    - For example the `GET /services/{id}` services by id endpoint, its `useIdFrom` should field value should be `"/services"` as this is the services list endpoint
    - This depends on some assumptions
        - That where there is an *item* by id endpoint there will also be an *item* list endpoint where ids can be found
        - That the id field can be found at `response["contents"][0]["id"]` in the  *item* list endpoint response data

## Example Test Profile

``` json
{
  "profile": "HSDS-Example-1.0",
  "testGroups": [ 
    {
      "name": "API info endpoint",
      "description": "Contains the API base endpoint, will validate the response schema",
      "messageLevel": "error",
      "required": true,
      "tests":[
        {
          "name": "API meta info - API & schema validation",
          "description": "Does the base endpoint return meta information about the API, and does it adhere to the schema",
          "endpoint": "/",
          "schema": "Example-1.0/api_details.json"
        }
      ]
    },
    {
      "name": "Services endpoints",
      "description": "Contains the service endpoints, will validate response schemas and pagination",
      "messageLevel": "error",
      "required": true,
      "tests":[
        {
          "name": "Services list - API & schema validation",
          "description": "Does the services list endpoint return a paginated list of services in the correct schema",
          "endpoint": "/services",
          "schema": "Example-1.0/service_list.json",
          "pagination": true
        },
        {
          "name": "Service by id - API & schema validation",
          "description": "Does the service by id endpoint return a fully nested service in the correct schema",
          "endpoint": "/services/",
          "schema": "Example-1.0/service.json",
          "useIdFrom": "/services-id"
        }
      ]
    },
    {
      "name": "Taxonomies endpoints",
      "description": "Will validate Taxonomies endpoints. Profile validation will not fail if these tests fail.",
      "messageLevel": "warning",
      "required": false,
      "tests":[
        {
          "name": "Taxonomies list - API & schema validation",
          "description": "Does the taxonomies list endpoint return a paginated list of taxonomies in the correct schema",
          "endpoint": "/taxonomies",
          "schema": "Example-1.0/taxonomy_list.json",
          "pagination": true
        },
        {
          "name": "Taxonomy by id - API & schema validation",
          "description": "Does the taxonomy by id endpoint return the full information of a taxonomy in the correct schema",
          "endpoint": "/taxonomies/",
          "schema": "Example-1.0/taxonomy.json",
          "useIdFrom": "/taxonomies-id"
        }
      ]
    }
  ]
}
```
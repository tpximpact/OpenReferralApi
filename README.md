# Open Referral UK API

For more information about the Open Referral UK project you can find the [documentation here](https://docs.openreferraluk.org/en/latest/)

## Deployment

The deployment to Heroku requires the [Heroku CLI](https://devcenter.heroku.com/articles/heroku-cli) and [Docker](https://www.docker.com/)

``` powershell
# Login to Heroku account
heroku login

# Login to Heroku container
heroku container:login

# Build the image
# docker build -t {{IMAGE_NAME}} -f OpenReferralApi/Dockerfile .
docker build -t oruk-api -f OpenReferralApi/Dockerfile .

# Tag the image to push to Heroku’s Container registry
# docker tag {{IMAGE_NAME}}:{{IMAGE_TAG}} registry.heroku.com/{{HEROKU_APP_NAME}}/web
docker tag oruk-api:latest registry.heroku.com/oruk-api/web

# Push the image to Heroku’s Container Registry
# docker push registry.heroku.com/{{HEROKU_APP_NAME}}/web
docker push registry.heroku.com/oruk-api/web

# Release the image to Heroku to be deployed
# heroku container:release web --app {{HEROKU_APP_NAME}}
heroku container:release web --app oruk-api
```

### Debugging

To debug the deployed app run the below

``` powershell
# Login to Heroku account
heroku login

# Run to see the logs of the deployment
# heroku logs --app={{HEROKU_APP_NAME}} --tail 
heroku logs --app=oruk-api --tail 
```

To run the API container locally

``` powershell

docker run -p 8080:8080 --name oruk-api oruk-api:latest

```

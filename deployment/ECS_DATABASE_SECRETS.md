# ECS database credential refresh

## Runtime configuration

Production settings enable the same authentication-failure refresh used locally.
The database remains the shared `forge-dev` RDS instance. The connection-string
secret supplies the host, database and TLS settings; the RDS-managed secret supplies
the current username and password. `/app/certs/global-bundle.pem` is included in the
container image and overrides the Windows development certificate path.

Production does not configure an AWS profile or ship local credentials. The AWS SDK
uses the ECS task role. `Forge:Secrets` controls API-key authentication and is
independent of these `Database` settings.

## Required AWS changes

The inspected service is `ambeeka-vending-api-svc` in `ambeeka-vending-cluster`,
region `us-east-1`, account `072531338071`. Its current task definition is
`ambeeka-vending-api:8` and the application container is `vending-api` on port 8080.

1. Add [the database secret policy](./ecs-database-secret-policy.json) as a new inline
   policy on **AmbeekaVendingAppTaskRole**. Preserve the existing logging policy.
   This is the application task role, not `AmbeekaECSTaskExecutionRole` or the
   GitHub image-publishing role. Both secrets currently use an AWS-managed KMS key;
   the explicit decrypt statement is scoped to the managed credentials secret.
2. Build and publish the new image. The existing GitHub workflow publishes an
   immutable commit-SHA image to `ambeeka-vending-ecr` on pushes to `main`; it does
   not update ECS. The `bootstrap` tag is not updated when it already exists.
3. Create a new revision of the service's task definition, preserving its other
   settings. Set container `vending-api` to the new commit-SHA image and retain
   `taskRoleArn` pointing to `AmbeekaVendingAppTaskRole`. Keep
   `ASPNETCORE_ENVIRONMENT=Production` and container port 8080.
4. Remove the `ConnectionStrings__VendingDatabase` entry from the container's
   `secrets` list in the new revision. It is no longer needed: the application
   reads Secrets Manager at runtime. The secret in AWS is retained.
5. Update `ambeeka-vending-api-svc` to the new task revision and wait for service
   stability. Keep the previous revision/image available for rollback; it still
   uses the old static credentials and may retain the original database failure.

The production JSON already supplies `Database:SecretId`,
`Database:CredentialsSecretId`, `Database:RootCertificate`, and `AWS:Region`.
Equivalent ECS environment overrides use double underscores, such as
`Database__CredentialsSecretId`. Do not set `AWS__Profile` or `AWS_PROFILE` in ECS.

The Fargate tasks need HTTPS access to Secrets Manager (through NAT or a suitable
VPC endpoint) and network access to RDS. Access available to the execution role
during container startup does not prove connectivity from the running task.

## Verification

- Confirm a new ECS deployment has reached its desired running count.
- Request `/health/ready` through the production API address and require HTTP 200.
  `/health` is only liveness and does not test database credentials or permissions.
- Open the pricing screen with a valid Admin token and check its database-backed
  request. Inspect `/ecs/ambeeka-vending-api` logs for AWS permission, networking,
  certificate or authentication failures. Do not log secret values.
- Normal successful requests must not fetch secrets again. The first failed
  connection with SQLSTATE `28P01` refreshes and retries opening once. Concurrent
  failures share a refresh, with a five-second lookup cooldown. SQL operations are
  not replayed. Other connection errors do not fetch secrets.

Local validation simulated an outdated password only in test memory and recovered
against RDS through EF. It did not rotate the live database or modify AWS secrets.
No live password rotation is required to deploy this change.

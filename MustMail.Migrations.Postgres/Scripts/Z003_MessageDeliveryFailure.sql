START TRANSACTION;
ALTER TABLE "Message" ADD "DeliveryFailed" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Message" ADD "DeliveryFailureReason" text;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260910145747_MessageDeliveryFailure', '10.0.12');

COMMIT;


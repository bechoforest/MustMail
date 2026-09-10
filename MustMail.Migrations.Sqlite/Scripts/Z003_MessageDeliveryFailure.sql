BEGIN TRANSACTION;
ALTER TABLE "Message" ADD "DeliveryFailed" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "Message" ADD "DeliveryFailureReason" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260910145709_MessageDeliveryFailure', '10.0.12');

COMMIT;


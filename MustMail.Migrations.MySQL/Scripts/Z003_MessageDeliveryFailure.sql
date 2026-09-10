START TRANSACTION;
ALTER TABLE `Message` ADD `DeliveryFailed` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `Message` ADD `DeliveryFailureReason` longtext NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260910145820_MessageDeliveryFailure', '10.0.12');

COMMIT;


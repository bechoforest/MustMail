START TRANSACTION;
ALTER TABLE `Message` DROP CONSTRAINT `FK_Message_User_UserId`;

DROP INDEX IX_Message_UserId ON Message;

ALTER TABLE `Message` DROP COLUMN `UserId`;

ALTER TABLE `Message` ADD `ContentStored` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `Message` ADD `SMTPAccountId` int NULL;

CREATE TABLE `MessageRecipient` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `MessageId` varchar(255) NOT NULL,
    `Email` varchar(254) NOT NULL,
    `Name` varchar(255) NULL,
    `Type` int NOT NULL,
    `Position` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_MessageRecipient_Message_MessageId` FOREIGN KEY (`MessageId`) REFERENCES `Message` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_Message_SMTPAccountId` ON `Message` (`SMTPAccountId`);

CREATE INDEX `IX_MessageRecipient_MessageId` ON `MessageRecipient` (`MessageId`);

ALTER TABLE `Message` ADD CONSTRAINT `FK_Message_SMTPAccount_SMTPAccountId` FOREIGN KEY (`SMTPAccountId`) REFERENCES `SMTPAccount` (`Id`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260831162337_MessageSmtpAccountAndRecipients', '10.0.11');

COMMIT;


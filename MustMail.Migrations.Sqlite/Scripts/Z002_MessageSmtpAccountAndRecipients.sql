BEGIN TRANSACTION;
DROP INDEX "IX_Message_UserId";

ALTER TABLE "Message" ADD "ContentStored" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "Message" ADD "SMTPAccountId" INTEGER NULL;

CREATE TABLE "MessageRecipient" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_MessageRecipient" PRIMARY KEY AUTOINCREMENT,
    "MessageId" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "Name" TEXT NULL,
    "Type" INTEGER NOT NULL,
    "Position" INTEGER NOT NULL,
    CONSTRAINT "FK_MessageRecipient_Message_MessageId" FOREIGN KEY ("MessageId") REFERENCES "Message" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Message_SMTPAccountId" ON "Message" ("SMTPAccountId");

CREATE INDEX "IX_MessageRecipient_MessageId" ON "MessageRecipient" ("MessageId");

CREATE TABLE "ef_temp_Message" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Message" PRIMARY KEY,
    "AttachmentCount" INTEGER NOT NULL,
    "ContentStored" INTEGER NOT NULL,
    "SMTPAccountId" INTEGER NULL,
    "SenderEmail" TEXT NOT NULL,
    "SenderName" TEXT NOT NULL,
    "Subject" TEXT NOT NULL,
    "Timestamp" TEXT NOT NULL,
    CONSTRAINT "FK_Message_SMTPAccount_SMTPAccountId" FOREIGN KEY ("SMTPAccountId") REFERENCES "SMTPAccount" ("Id")
);

INSERT INTO "ef_temp_Message" ("Id", "AttachmentCount", "ContentStored", "SMTPAccountId", "SenderEmail", "SenderName", "Subject", "Timestamp")
SELECT "Id", "AttachmentCount", "ContentStored", "SMTPAccountId", "SenderEmail", "SenderName", "Subject", "Timestamp"
FROM "Message";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "Message";

ALTER TABLE "ef_temp_Message" RENAME TO "Message";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_Message_SMTPAccountId" ON "Message" ("SMTPAccountId");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260831162219_MessageSmtpAccountAndRecipients', '10.0.11');


BEGIN TRANSACTION;
ALTER TABLE [Message] DROP CONSTRAINT [FK_Message_User_UserId];

DROP INDEX [IX_Message_UserId] ON [Message];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Message]') AND [c].[name] = N'UserId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Message] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Message] DROP COLUMN [UserId];

ALTER TABLE [Message] ADD [ContentStored] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Message] ADD [SMTPAccountId] int NULL;

CREATE TABLE [MessageRecipient] (
    [Id] int NOT NULL IDENTITY,
    [MessageId] nvarchar(255) NOT NULL,
    [Email] nvarchar(254) NOT NULL,
    [Name] nvarchar(255) NULL,
    [Type] int NOT NULL,
    [Position] int NOT NULL,
    CONSTRAINT [PK_MessageRecipient] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MessageRecipient_Message_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [Message] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Message_SMTPAccountId] ON [Message] ([SMTPAccountId]);

CREATE INDEX [IX_MessageRecipient_MessageId] ON [MessageRecipient] ([MessageId]);

ALTER TABLE [Message] ADD CONSTRAINT [FK_Message_SMTPAccount_SMTPAccountId] FOREIGN KEY ([SMTPAccountId]) REFERENCES [SMTPAccount] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260831162431_MessageSmtpAccountAndRecipients', N'10.0.11');

COMMIT;
GO


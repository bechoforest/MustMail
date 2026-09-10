BEGIN TRANSACTION;
ALTER TABLE [Message] ADD [DeliveryFailed] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Message] ADD [DeliveryFailureReason] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260910145925_MessageDeliveryFailure', N'10.0.12');

COMMIT;
GO


CREATE TABLE dbo.BatchData (
	MessageId uniqueidentifier NOT NULL
	 CONSTRAINT DF__dbo_BatchData__MessageID DEFAULT (NEWSEQUENTIALID()),
	MessageTimestamp datetime NOT NULL
	 CONSTRAINT DF__dbo_BatchData__MessageTimestamp DEFAULT (GETDATE()),
	SeedVal int NOT NULL
);
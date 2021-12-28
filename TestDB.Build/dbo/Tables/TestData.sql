CREATE TABLE dbo.TestData (
	MessageId uniqueidentifier NOT NULL
	 CONSTRAINT DF__dbo_TestData__MessageID DEFAULT (NEWSEQUENTIALID()),
	MessageTimestamp datetime NOT NULL
	 CONSTRAINT DF__dbo_TestData__MessageTimestamp DEFAULT (GETDATE()),
	SeedVal int NOT NULL
);
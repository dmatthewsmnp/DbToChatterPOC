CREATE TABLE dbo.OutboxMessage (
	Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
	BatchId uniqueidentifier NULL,
	Destination nvarchar(max) NOT NULL,
	MessageBody nvarchar(max) NOT NULL,
	MessageContentType nvarchar(max) NOT NULL,
	MessageContext nvarchar(max) NOT NULL,
	MessageId nvarchar(max) NOT NULL,
	ProcessedFromOutboxAtUtc datetime2 NULL,
	SentToOutboxAtUtc datetime2 NULL
);
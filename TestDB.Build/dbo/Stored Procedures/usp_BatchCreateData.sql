CREATE PROCEDURE dbo.usp_BatchCreateData
	@CorrelationId varchar(50) = NULL,
	@BatchID uniqueidentifier = NULL OUTPUT
AS
-- Test proc to generate a batch of Outbox messages
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @errcode int, @rowcount int;
DECLARE @tbl_Output TABLE (MessageId uniqueidentifier NOT NULL, SeedVal int NOT NULL);

BEGIN TRY

	BEGIN TRANSACTION tr_BatchCreateData;

	DECLARE @start int = 1, @end int = 20;
	WITH NumberSequence(Number) AS (
		SELECT @start AS Number
		UNION ALL
		SELECT Number + 1
			FROM NumberSequence
			WHERE Number < @end
	)
	INSERT INTO dbo.BatchData (SeedVal)
	 OUTPUT INSERTED.MessageId, INSERTED.SeedVal INTO @tbl_Output
	 SELECT Number FROM NumberSequence;
	SELECT @errcode = @@ERROR, @rowcount = @@ROWCOUNT;
	IF @errcode <> 0 OR @rowcount = 0 RAISERROR('Error inserting test data (1-%d-%d)', 15, 1, @errcode, @rowcount);

	SET @BatchID = NEWID();

	INSERT INTO dbo.OutboxMessage (
		BatchId,
		Destination,
		MessageBody,
		MessageContentType,
		MessageContext,
		MessageId,
		SentToOutboxAtUtc
	)
	SELECT @BatchID,
		'test-chatter', -- Target queue name should be configurable
		(SELECT o.MessageId, o.SeedVal FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), -- Sample data
		'application/json',
		'{"Chatter.ContentType":"application/json","Chatter.CorrelationId":"' + ISNULL(NULLIF(@CorrelationId, ''), CONVERT(varchar(50), NEWID())) + '"}',
		o.MessageId,
		GETUTCDATE()
	FROM @tbl_Output o;
	SELECT @errcode = @@ERROR, @rowcount = @@ROWCOUNT;
	IF @errcode <> 0 OR @rowcount = 0 RAISERROR('Error inserting outbox record (2-%d-%d)', 15, 2, @errcode, @rowcount);

	COMMIT TRANSACTION tr_BatchCreateData;
	RETURN 201; -- HTTP 201 Created

END TRY
BEGIN CATCH
	ROLLBACK TRANSACTION tr_BatchCreateData;
	THROW;
END CATCH
GO
GRANT EXECUTE ON dbo.usp_BatchCreateData TO public;
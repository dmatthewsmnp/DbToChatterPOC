CREATE PROCEDURE dbo.usp_MaybeCreateData
	@SeedVal int,
	@CorrelationId varchar(50) = NULL
AS
-- Test proc to create (or not create) test data and post to outbox
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- If seed value is odd, no action required:
IF (@SeedVal % 2) = 1 RETURN 204; -- HTTP No Content

DECLARE @errcode int, @rowcount int;
DECLARE @tbl_Output TABLE (MessageId uniqueidentifier NOT NULL, SeedVal int NOT NULL);

BEGIN TRY

	BEGIN TRANSACTION tr_MaybeCreateData;

	INSERT INTO dbo.TestData (SeedVal)
	 OUTPUT INSERTED.MessageId, INSERTED.SeedVal INTO @tbl_Output
	 VALUES (@SeedVal);
	SELECT @errcode = @@ERROR, @rowcount = @@ROWCOUNT;
	IF @errcode <> 0 OR @rowcount <> 1 RAISERROR('Error inserting test data (1-%d-%d)', 15, 1, @errcode, @rowcount);

	INSERT INTO dbo.OutboxMessage (
		BatchId,
		Destination,
		MessageBody,
		MessageContentType,
		MessageContext,
		MessageId,
		SentToOutboxAtUtc
	)
	SELECT NEWID(),
		'test-chatter', -- Target queue name should be configurable
		(SELECT o.MessageId, o.SeedVal, 'Single' [Method] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), -- Sample data
		'application/json',
		'{"Chatter.ContentType":"application/json","Chatter.CorrelationId":"' + ISNULL(NULLIF(@CorrelationId, ''), CONVERT(varchar(50), NEWID())) + '"}',
		o.MessageId,
		GETUTCDATE()
	FROM @tbl_Output o;
	SELECT @errcode = @@ERROR, @rowcount = @@ROWCOUNT;
	IF @errcode <> 0 OR @rowcount <> 1 RAISERROR('Error inserting outbox record (2-%d-%d)', 15, 2, @errcode, @rowcount);

	COMMIT TRANSACTION tr_MaybeCreateData;
	RETURN 201; -- HTTP 201 Created

END TRY
BEGIN CATCH
	ROLLBACK TRANSACTION tr_MaybeCreateData;
	THROW;
END CATCH
GO
GRANT EXECUTE ON dbo.usp_MaybeCreateData TO public;
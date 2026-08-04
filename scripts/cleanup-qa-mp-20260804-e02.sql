SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Execute bit = 0;

DECLARE @TargetGifts table
(
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    ExpectedName nvarchar(120) NOT NULL
);

INSERT INTO @TargetGifts (Id, ExpectedName)
VALUES
    ('f464b705-d54b-4969-a3b2-567f73d662c7', N'QA-MP-20260804-E02-GIFT-001-MAIN'),
    ('88cb50c8-f76d-45e5-b3ad-794dcc2247ae', N'QA-MP-20260804-E02-GIFT-002-CONCURRENCY'),
    ('e4a6a04d-e556-446f-ad49-e2f0789f1cbf', N'QA-MP-20260804-E02-GIFT-003-CACHE-UPDATED');

IF EXISTS
(
    SELECT 1
    FROM @TargetGifts target
    LEFT JOIN Gifts gift ON gift.Id = target.Id
    WHERE gift.Id IS NOT NULL AND gift.Name <> target.ExpectedName
)
    THROW 51000, 'A gift ID no longer matches the exact QA name. Cleanup aborted.', 1;

DECLARE @PaymentIds table (Id uniqueidentifier NOT NULL PRIMARY KEY);
DECLARE @ContributionIds table (Id uniqueidentifier NOT NULL PRIMARY KEY);
DECLARE @GuestInvitationIds table (Id uniqueidentifier NOT NULL PRIMARY KEY);
DECLARE @GuestConfirmationIds table (Id uniqueidentifier NOT NULL PRIMARY KEY);
DECLARE @UserIds table (Id uniqueidentifier NOT NULL PRIMARY KEY);

INSERT INTO @PaymentIds (Id)
SELECT payment.Id
FROM Payments payment
JOIN @TargetGifts gift ON gift.Id = payment.GiftId;

INSERT INTO @ContributionIds (Id)
SELECT contribution.Id
FROM Contributions contribution
JOIN @TargetGifts gift ON gift.Id = contribution.GiftId;

INSERT INTO @GuestInvitationIds (Id)
SELECT invitation.Id
FROM GuestInvitations invitation
WHERE invitation.Name = N'QA-MP-20260804-E02-GUEST-001';

INSERT INTO @GuestConfirmationIds (Id)
SELECT DISTINCT confirmed.GuestConfirmationId
FROM ConfirmedGuests confirmed
WHERE confirmed.Name = N'QA-MP-20260804-E02-GUEST-001';

INSERT INTO @UserIds (Id)
SELECT appUser.Id
FROM Users appUser
WHERE appUser.Name = N'QA-MP-20260804-E02-USER-001';

SELECT N'Gifts' AS RecordType, COUNT(*) AS Records
FROM Gifts gift JOIN @TargetGifts target ON target.Id = gift.Id
UNION ALL
SELECT N'Payments', COUNT(*) FROM @PaymentIds
UNION ALL
SELECT N'Contributions', COUNT(*) FROM @ContributionIds
UNION ALL
SELECT N'EmailOutboxMessages', COUNT(*) FROM EmailOutboxMessages item JOIN @PaymentIds target ON target.Id = item.PaymentId
UNION ALL
SELECT N'PaymentRefundOperations', COUNT(*) FROM PaymentRefundOperations item JOIN @PaymentIds target ON target.Id = item.PaymentId
UNION ALL
SELECT N'PaymentOrderLookupTokens', COUNT(*) FROM PaymentOrderLookupTokens item JOIN @PaymentIds target ON target.Id = item.PaymentId
UNION ALL
SELECT N'GuestInvitations', COUNT(*) FROM @GuestInvitationIds
UNION ALL
SELECT N'GuestConfirmations', COUNT(*) FROM @GuestConfirmationIds
UNION ALL
SELECT N'ConfirmedGuests', COUNT(*) FROM ConfirmedGuests item JOIN @GuestConfirmationIds target ON target.Id = item.GuestConfirmationId
UNION ALL
SELECT N'Users', COUNT(*) FROM @UserIds;

IF @Execute = 0
BEGIN
    SELECT N'PREVIEW_ONLY' AS CleanupStatus, N'Set @Execute = 1 only after preserving the audit evidence.' AS NextAction;
    RETURN;
END;

BEGIN TRANSACTION;

DELETE item
FROM EmailOutboxMessages item
JOIN @PaymentIds target ON target.Id = item.PaymentId;

DELETE item
FROM PaymentRefundOperations item
JOIN @PaymentIds target ON target.Id = item.PaymentId;

DELETE item
FROM PaymentOrderLookupTokens item
JOIN @PaymentIds target ON target.Id = item.PaymentId;

DELETE audit
FROM AuditLogs audit
WHERE audit.EntityId IN
(
    SELECT CONVERT(nvarchar(100), Id) FROM @PaymentIds
    UNION ALL
    SELECT CONVERT(nvarchar(100), Id) FROM @ContributionIds
    UNION ALL
    SELECT CONVERT(nvarchar(100), Id) FROM @TargetGifts
    UNION ALL
    SELECT CONVERT(nvarchar(100), Id) FROM @GuestInvitationIds
    UNION ALL
    SELECT CONVERT(nvarchar(100), Id) FROM @GuestConfirmationIds
    UNION ALL
    SELECT CONVERT(nvarchar(100), Id) FROM @UserIds
);

DELETE payment
FROM Payments payment
JOIN @PaymentIds target ON target.Id = payment.Id;

DELETE contribution
FROM Contributions contribution
JOIN @ContributionIds target ON target.Id = contribution.Id;

DELETE confirmed
FROM ConfirmedGuests confirmed
JOIN @GuestConfirmationIds target ON target.Id = confirmed.GuestConfirmationId;

DELETE confirmation
FROM GuestConfirmations confirmation
JOIN @GuestConfirmationIds target ON target.Id = confirmation.Id;

DELETE invitation
FROM GuestInvitations invitation
JOIN @GuestInvitationIds target ON target.Id = invitation.Id;

DELETE token
FROM RefreshTokens token
JOIN @UserIds target ON target.Id = token.UserId;

DELETE appUser
FROM Users appUser
JOIN @UserIds target ON target.Id = appUser.Id;

DELETE gift
FROM Gifts gift
JOIN @TargetGifts target ON target.Id = gift.Id AND target.ExpectedName = gift.Name;

COMMIT TRANSACTION;

SELECT N'EXECUTED' AS CleanupStatus,
       (SELECT COUNT(*) FROM Gifts gift JOIN @TargetGifts target ON target.Id = gift.Id) AS RemainingGifts,
       (SELECT COUNT(*) FROM Payments payment JOIN @PaymentIds target ON target.Id = payment.Id) AS RemainingPayments,
       (SELECT COUNT(*) FROM Contributions contribution JOIN @ContributionIds target ON target.Id = contribution.Id) AS RemainingContributions,
       (SELECT COUNT(*) FROM Users appUser JOIN @UserIds target ON target.Id = appUser.Id) AS RemainingUsers;

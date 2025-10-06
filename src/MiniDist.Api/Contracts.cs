namespace MiniDist.Api;


public record ClaimCreated(int ClaimId, string PolicyNumber, decimal Amount, DateTime AtUtc);
public record PaymentReceived(int ClaimId, decimal Amount, DateTime AtUtc);
public record PaymentFailed(int ClaimId, string Reason, DateTime AtUtc);
public record ClaimActivated(int ClaimId, DateTime AtUtc);


public record ClaimReverted(int ClaimId, string Reason, DateTime AtUtc);

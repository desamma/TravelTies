namespace TravelTies.Areas.Customer.Types;

public record Response(
    int error,
    String message,
    object? data
);
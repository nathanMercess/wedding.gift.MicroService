namespace wedding.gift.Crosscutting.Constants;

public static class ErrorCodes
{
    public const string BAD_REQUEST = "BAD_REQUEST";
    public const string UNAUTHORIZED = "UNAUTHORIZED";
    public const string FORBIDDEN = "FORBIDDEN";
    public const string NOT_FOUND = "NOT_FOUND";
    public const string HTTP_ERROR = "HTTP_ERROR";
    public const string VALIDATION_ERROR = "VALIDATION_ERROR";
    public const string FIELD_INVALID = "FIELD_INVALID";
    public const string UNHANDLED_ERROR = "UNHANDLED_ERROR";

    public const string REQUIRED_FIELDS = "REQUIRED_FIELDS";
    public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
    public const string USER_INACTIVE = "USER_INACTIVE";
    public const string EMAIL_NOT_CONFIRMED = "EMAIL_NOT_CONFIRMED";
    public const string EMAIL_ALREADY_EXISTS = "EMAIL_ALREADY_EXISTS";
    public const string USER_NOT_FOUND = "USER_NOT_FOUND";
    public const string INVALID_CONFIRMATION_TOKEN = "INVALID_CONFIRMATION_TOKEN";
    public const string INVALID_JWT_CONFIGURATION = "INVALID_JWT_CONFIGURATION";

    public const string INVALID_CONTRIBUTION_STATUS = "INVALID_CONTRIBUTION_STATUS";
    public const string CONTRIBUTION_NOT_FOUND = "CONTRIBUTION_NOT_FOUND";

    public const string GIFT_NOT_FOUND = "GIFT_NOT_FOUND";
    public const string GIFT_UNAVAILABLE = "GIFT_UNAVAILABLE";
    public const string INVALID_GIFT_PAGE = "INVALID_GIFT_PAGE";
    public const string INVALID_GIFT_PAGE_SIZE = "INVALID_GIFT_PAGE_SIZE";

    public const string INVALID_DASHBOARD_DAYS = "INVALID_DASHBOARD_DAYS";
    public const string INVALID_DASHBOARD_RECENT_ITEMS = "INVALID_DASHBOARD_RECENT_ITEMS";

    public const string INVALID_IMAGE_FILE = "INVALID_IMAGE_FILE";
    public const string IMAGE_FILE_TOO_LARGE = "IMAGE_FILE_TOO_LARGE";
    public const string INVALID_IMAGE_CONTENT_TYPE = "INVALID_IMAGE_CONTENT_TYPE";

    public const string INVALID_PRODUCT_URL = "INVALID_PRODUCT_URL";
    public const string PRODUCT_URL_UNREACHABLE = "PRODUCT_URL_UNREACHABLE";

    public const string INVALID_BOOTSTRAP_ADMIN_ROLE = "INVALID_BOOTSTRAP_ADMIN_ROLE";
    public const string UNAUTHORIZED_WEBHOOK = "UNAUTHORIZED_WEBHOOK";
}

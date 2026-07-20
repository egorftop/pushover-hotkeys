using System.Net;
using System.Net.Http;
using System.Text.Json;
using PushoverHotkeys.Models;

namespace PushoverHotkeys.Services;

public sealed class PushoverClient
{
    private const string MessageEndpoint = "https://api.pushover.net/1/messages.json";
    private readonly HttpClient _httpClient;

    public PushoverClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public Task<SendResult> SendAsync(
        string appToken,
        HotkeyBinding binding,
        CancellationToken cancellationToken = default) =>
        SendAsync(appToken, binding.Recipients, binding.Message, binding.Priority, binding.Sound, cancellationToken);

    public async Task<SendResult> SendAsync(
        string appToken,
        IEnumerable<Recipient> recipients,
        string message,
        int priority,
        string sound,
        CancellationToken cancellationToken = default)
    {
        if (!PushoverKeyValidator.IsValid(appToken))
        {
            return SendResult.Failure("Укажите корректный Pushover App Token.");
        }

        var keys = recipients
            .Select(recipient => recipient.UserKey.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (keys.Count is < 1 or > 50 || keys.Any(key => !PushoverKeyValidator.IsValid(key)))
        {
            return SendResult.Failure("В привязке должен быть от 1 до 50 корректных Pushover Key.");
        }

        message = message.Trim();
        if (message.Length is < 1 or > 1024)
        {
            return SendResult.Failure("Сообщение должно содержать от 1 до 1024 символов.");
        }

        if (!Enum.IsDefined(typeof(PushoverPriority), priority))
        {
            return SendResult.Failure("Выбран недопустимый приоритет Pushover.");
        }

        if (!PushoverSounds.IsValid(sound))
        {
            return SendResult.Failure("Выбран недопустимый звук Pushover.");
        }

        var formValues = new Dictionary<string, string>
        {
            ["token"] = appToken,
            ["user"] = string.Join(',', keys),
            ["message"] = message,
            ["priority"] = priority.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (sound != PushoverSounds.DefaultId)
        {
            formValues["sound"] = sound;
        }

        using var content = new FormUrlEncodedContent(formValues);

        try
        {
            using var response = await _httpClient.PostAsync(MessageEndpoint, content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.OK && HasSuccessStatus(body))
            {
                return SendResult.Success();
            }

            return SendResult.Failure(ReadPushoverError(body, response.StatusCode));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SendResult.Failure("Pushover не ответил в течение 10 секунд.");
        }
        catch (HttpRequestException)
        {
            return SendResult.Failure("Не удалось подключиться к Pushover. Проверьте интернет-соединение.");
        }
        catch (Exception)
        {
            return SendResult.Failure("Непредвиденная ошибка при отправке в Pushover.");
        }
    }

    private static bool HasSuccessStatus(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("status", out var status) && status.GetInt32() == 1;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ReadPushoverError(string body, HttpStatusCode statusCode)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var messages = errors.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(message => !string.IsNullOrWhiteSpace(message));
                var message = string.Join(" ", messages);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return $"Pushover отклонил запрос: {message}";
                }
            }
        }
        catch (JsonException)
        {
            // The HTTP status still provides a useful, non-sensitive error below.
        }

        return $"Pushover вернул ошибку HTTP {(int)statusCode}.";
    }
}

public sealed record SendResult(bool IsSuccess, string ErrorMessage)
{
    public static SendResult Success() => new(true, string.Empty);
    public static SendResult Failure(string errorMessage) => new(false, errorMessage);
}

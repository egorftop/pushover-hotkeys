namespace PushoverHotkeys.Models;

public static class BindingMerger
{
    public static void Upsert(List<HotkeyBinding> bindings, HotkeyBinding candidate)
    {
        if (!candidate.Chord.IsValid)
        {
            throw new ArgumentException("Не выбрана горячая клавиша.", nameof(candidate));
        }

        candidate.Message = candidate.Message.Trim();
        if (candidate.Message.Length is < 1 or > 1024)
        {
            throw new ArgumentException("Сообщение должно содержать от 1 до 1024 символов.", nameof(candidate));
        }

        if (!Enum.IsDefined(typeof(PushoverPriority), candidate.Priority))
        {
            throw new ArgumentException("Выбран недопустимый приоритет Pushover.", nameof(candidate));
        }

        if (!PushoverSounds.IsValid(candidate.Sound))
        {
            throw new ArgumentException("Выбран недопустимый звук Pushover.", nameof(candidate));
        }

        candidate.Recipients = candidate.Recipients
            .GroupBy(recipient => recipient.UserKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (candidate.Recipients.Count is < 1 or > 50)
        {
            throw new ArgumentException("В привязке должен быть от 1 до 50 получателей.", nameof(candidate));
        }

        if (candidate.Recipients.Any(recipient => !PushoverKeyValidator.IsValid(recipient.UserKey)))
        {
            throw new ArgumentException("Один или несколько Pushover Key имеют неверный формат.", nameof(candidate));
        }

        var matchingBinding = bindings.FirstOrDefault(binding =>
            binding.Id != candidate.Id && binding.Chord.Equals(candidate.Chord));

        if (matchingBinding is not null)
        {
            var mergedRecipients = matchingBinding.Recipients
                .Concat(candidate.Recipients)
                .GroupBy(recipient => recipient.UserKey, StringComparer.Ordinal)
                .Select(group => group.First().DeepCopy())
                .ToList();

            if (mergedRecipients.Count > 50)
            {
                throw new ArgumentException("После объединения в привязке будет больше 50 получателей.", nameof(candidate));
            }

            matchingBinding.Recipients = mergedRecipients;
            bindings.RemoveAll(binding => binding.Id == candidate.Id);
            return;
        }

        var index = bindings.FindIndex(binding => binding.Id == candidate.Id);
        if (index >= 0)
        {
            bindings[index] = candidate.DeepCopy();
        }
        else
        {
            bindings.Add(candidate.DeepCopy());
        }
    }
}

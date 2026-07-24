using System;
using UnityEngine;

/// <summary>Single-type log fuel storage with partial-log burn progress.</summary>
[Serializable]
public sealed class ProcessingFuelBank
{
    private string _itemId = "";
    private int _logCount;
    private float _secondsBurnedFromCurrentLog;

    public string StoredItemId => _itemId ?? "";
    public int StoredAmount => Mathf.Max(0, _logCount);
    public float SecondsBurnedFromCurrentLog => Mathf.Max(0f, _secondsBurnedFromCurrentLog);
    public bool HasFuel => _logCount > 0;

    public float GetSecondsRemaining()
    {
        if (_logCount <= 0)
            return 0f;

        if (!ProcessingFuelCatalog.TryGetSecondsPerLog(_itemId, out float perLog))
            return 0f;

        return (_logCount - 1) * perLog + (perLog - _secondsBurnedFromCurrentLog);
    }

    public bool CanAccept(string itemId, int amount, out string failureReason)
    {
        failureReason = null;
        if (amount <= 0)
        {
            failureReason = "Invalid deposit.";
            return false;
        }

        if (!ProcessingFuelCatalog.IsValidFuelLog(itemId))
        {
            failureReason = "Only logs can be used as fuel.";
            return false;
        }

        string normalized = itemId.Trim().ToLowerInvariant();
        if (_logCount > 0 &&
            !string.Equals(_itemId, normalized, StringComparison.OrdinalIgnoreCase))
        {
            failureReason = "This station already holds a different log type.";
            return false;
        }

        if (_logCount >= ProcessingFuelCatalog.MaxFuelLogs)
        {
            failureReason = $"Fuel slot is full (max {ProcessingFuelCatalog.MaxFuelLogs}).";
            return false;
        }

        return true;
    }

    public int GetDepositCapacity(int requestedAmount) =>
        Mathf.Clamp(requestedAmount, 0, ProcessingFuelCatalog.MaxFuelLogs - StoredAmount);

    public void AddLogs(string itemId, int amount)
    {
        if (amount <= 0 || !ProcessingFuelCatalog.IsValidFuelLog(itemId))
            return;

        _itemId = itemId.Trim().ToLowerInvariant();
        _logCount = Mathf.Clamp(_logCount + amount, 0, ProcessingFuelCatalog.MaxFuelLogs);
        if (_logCount <= 0)
            Clear();
    }

    public int RemoveAllLogs()
    {
        int amount = _logCount;
        Clear();
        return amount;
    }

    /// <summary>
    /// Withdraws only whole unburned logs. Keeps a partially burned current log in the bank
    /// so players cannot refund burn progress as a full item.
    /// </summary>
    public int WithdrawFullLogsOnly()
    {
        if (_logCount <= 0)
            return 0;

        if (_secondsBurnedFromCurrentLog > 0.0001f)
        {
            int fullLogs = _logCount - 1;
            _logCount = 1;
            return Mathf.Max(0, fullLogs);
        }

        int amount = _logCount;
        Clear();
        return amount;
    }

    public bool TryConsumeSeconds(float seconds)
    {
        if (seconds <= 0f)
            return true;

        if (_logCount <= 0)
            return false;

        if (!ProcessingFuelCatalog.TryGetSecondsPerLog(_itemId, out float perLog))
            return false;

        float remaining = seconds;
        while (remaining > 0.0001f)
        {
            if (_logCount <= 0)
                return false;

            float leftOnCurrentLog = perLog - _secondsBurnedFromCurrentLog;
            if (remaining < leftOnCurrentLog)
            {
                _secondsBurnedFromCurrentLog += remaining;
                return true;
            }

            remaining -= leftOnCurrentLog;
            _logCount--;
            _secondsBurnedFromCurrentLog = 0f;
        }

        ClearIfEmpty();
        return true;
    }

    public void Load(string itemId, int logCount, float secondsBurnedFromCurrentLog)
    {
        _itemId = string.IsNullOrWhiteSpace(itemId) ? "" : itemId.Trim().ToLowerInvariant();
        _logCount = Mathf.Clamp(logCount, 0, ProcessingFuelCatalog.MaxFuelLogs);
        _secondsBurnedFromCurrentLog = Mathf.Max(0f, secondsBurnedFromCurrentLog);

        if (_logCount <= 0 || !ProcessingFuelCatalog.IsValidFuelLog(_itemId))
            Clear();
        else if (ProcessingFuelCatalog.TryGetSecondsPerLog(_itemId, out float perLog))
            _secondsBurnedFromCurrentLog = Mathf.Clamp(_secondsBurnedFromCurrentLog, 0f, perLog - 0.0001f);
    }

    public void Clear()
    {
        _itemId = "";
        _logCount = 0;
        _secondsBurnedFromCurrentLog = 0f;
    }

    private void ClearIfEmpty()
    {
        if (_logCount <= 0)
            Clear();
    }
}

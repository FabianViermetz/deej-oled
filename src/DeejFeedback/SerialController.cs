using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;

namespace DeejFeedback;

public sealed class SerialController : IDisposable
{
    private SerialPort? _port;
    private readonly StringBuilder _buffer = new();
    private readonly object _writeLock = new();
    private int _lastButtonSequence = -1;
    public bool IsConnected => _port?.IsOpen == true;
    public event Action<int[]>? FadersReceived;
    public event Action? MicButtonPressed;
    public event Action<bool>? MicButtonStateReceived;
    public event Action<int>? MicButtonCommandReceived;
    public event Action<string>? Log;

    public static string[] AvailablePorts() => SerialPort.GetPortNames().OrderBy(x => x).ToArray();

    public void Connect(string portName, int baudRate)
    {
        Disconnect();
        _port = new SerialPort(portName, baudRate) { NewLine = "\n", ReadTimeout = 500, WriteTimeout = 500, DtrEnable = true };
        _lastButtonSequence = -1;
        _port.DataReceived += OnDataReceived;
        _port.Open();
        Log?.Invoke($"Controller connected: {portName} @ {baudRate}");
    }

    public void Disconnect()
    {
        if (_port is null) return;
        try { _port.DataReceived -= OnDataReceived; if (_port.IsOpen) _port.Close(); } catch { }
        _port.Dispose();
        _port = null;
    }

    public void SendFeedback(IReadOnlyList<int> volumes, PrivacyState privacy, int displaySleepSeconds, int heartbeatMs, int feedbackMs)
    {
        if (!IsConnected || _port is null) return;
        var state = privacy switch { PrivacyState.MutedConfirmed => 1, PrivacyState.Active => 0, _ => 2 };
        var values = string.Join('|', volumes.Take(4));
        try { lock (_writeLock) _port.WriteLine($"S|{values}|{state}|{displaySleepSeconds}|{heartbeatMs}|{feedbackMs}"); }
        catch (Exception ex) { Log?.Invoke("Send failed: " + ex.Message); Disconnect(); }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs args)
    {
        try
        {
            if (_port is null) return;
            _buffer.Append(_port.ReadExisting());
            while (true)
            {
                var text = _buffer.ToString();
                var newline = text.IndexOf('\n');
                if (newline < 0) break;
                var line = text[..newline].Trim();
                _buffer.Remove(0, newline + 1);
                ParseLine(line);
            }
        }
        catch (Exception ex) { Log?.Invoke("Receive error: " + ex.Message); }
    }

    private void ParseLine(string line)
    {
        var fields = line.Split('|');
        if (fields.Length >= 5 && fields[0] == "F" && fields.Skip(1).Take(4).All(x => int.TryParse(x, out var value) && value >= 0 && value <= 1023))
        {
            FadersReceived?.Invoke(fields.Skip(1).Take(4).Select(int.Parse).ToArray());
            if (fields.Length >= 6 && int.TryParse(fields[5], out var pressed))
                MicButtonStateReceived?.Invoke(pressed != 0);
        }
        else if (fields.Length >= 3 && fields[0] == "B" && fields[1] == "MIC" && int.TryParse(fields[2], out var sequence))
        {
            SendButtonAcknowledgement(sequence);
            if (sequence != _lastButtonSequence)
            {
                _lastButtonSequence = sequence;
                MicButtonCommandReceived?.Invoke(sequence);
            }
        }
        else if (fields.Length >= 3 && fields[0] == "B" && fields[1] == "MIC" && fields[2] == "PRESS")
            MicButtonPressed?.Invoke();
        else if (fields.Length >= 2 && fields[0] == "HELLO")
        {
            _lastButtonSequence = -1;
            Log?.Invoke("Firmware: " + string.Join(' ', fields.Skip(1)));
        }
    }

    private void SendButtonAcknowledgement(int sequence)
    {
        if (!IsConnected || _port is null) return;
        try { lock (_writeLock) _port.WriteLine($"A|MIC|{sequence}"); }
        catch (Exception ex) { Log?.Invoke("Button acknowledgement failed: " + ex.Message); }
    }

    public void Dispose() => Disconnect();
}

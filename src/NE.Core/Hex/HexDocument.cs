using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace NEManager.Core.Hex;

/// <summary>
/// 十六进制文档，支持文件加载、编辑、撤销/重做。
/// </summary>
public class HexDocument : INotifyPropertyChanged
{
    private byte[] _data = Array.Empty<byte>();
    private string _filePath = string.Empty;
    private bool _isModified;
    private string _title = string.Empty;

    private readonly Stack<UndoEntry> _undoStack = new();
    private readonly Stack<UndoEntry> _redoStack = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public byte[] Data
    {
        get => _data;
        private set => _data = value;
    }

    public long Length => _data.Length;

    public string FilePath
    {
        get => _filePath;
        private set
        {
            _filePath = value;
            OnPropertyChanged();
        }
    }

    public bool IsModified
    {
        get => _isModified;
        private set
        {
            _isModified = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => _title;
        private set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    private const long MaxFileSize = 100L * 1024 * 1024; // 100 MB

    public void LoadFromFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FileNotFoundException("文件不存在。", path);
        if (info.Length > MaxFileSize)
            throw new IOException($"文件大小超过 100 MB 限制（当前 {info.Length} 字节）。");

        _data = File.ReadAllBytes(path);
        FilePath = path;
        Title = info.Name;
        IsModified = false;
        _undoStack.Clear();
        _redoStack.Clear();
        OnPropertyChanged(nameof(Length));
    }

    public void SaveToFile(string path)
    {
        File.WriteAllBytes(path, _data);
        FilePath = path;
        Title = Path.GetFileName(path);
        IsModified = false;
    }

    public byte[] GetBytes(long offset, int count)
    {
        if (offset < 0 || offset >= _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        int available = (int)Math.Min(count, _data.Length - offset);
        var result = new byte[available];
        Array.Copy(_data, offset, result, 0, available);
        return result;
    }

    public void SetByte(long offset, byte value)
    {
        if (offset < 0 || offset >= _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        byte old = _data[offset];
        if (old == value) return;

        _data[offset] = value;
        PushUndo(new UndoEntry(offset, old, value, UndoKind.Set));
        MarkModified();
    }

    public void SetBytes(long offset, byte[] data)
    {
        if (offset < 0 || offset + data.Length > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var oldData = new byte[data.Length];
        Array.Copy(_data, offset, oldData, 0, data.Length);
        Array.Copy(data, 0, _data, offset, data.Length);
        PushUndo(new UndoEntry(offset, oldData, data, UndoKind.SetRange));
        MarkModified();
    }

    public void InsertBytes(long offset, byte[] data)
    {
        if (offset < 0 || offset > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var newData = new byte[_data.Length + data.Length];
        Array.Copy(_data, 0, newData, 0, offset);
        Array.Copy(data, 0, newData, offset, data.Length);
        Array.Copy(_data, offset, newData, offset + data.Length, _data.Length - offset);
        _data = newData;
        PushUndo(new UndoEntry(offset, data, null, UndoKind.Insert));
        MarkModified();
        OnPropertyChanged(nameof(Length));
    }

    public void DeleteBytes(long offset, int count)
    {
        if (offset < 0 || offset + count > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var removed = new byte[count];
        Array.Copy(_data, offset, removed, 0, count);

        var newData = new byte[_data.Length - count];
        Array.Copy(_data, 0, newData, 0, offset);
        Array.Copy(_data, offset + count, newData, offset, _data.Length - offset - count);
        _data = newData;
        PushUndo(new UndoEntry(offset, removed, null, UndoKind.Delete));
        MarkModified();
        OnPropertyChanged(nameof(Length));
    }

    public long FindBytes(byte[] pattern, long startOffset, bool forward)
    {
        if (pattern.Length == 0) return -1;
        if (forward)
        {
            for (long i = startOffset; i <= _data.Length - pattern.Length; i++)
            {
                if (MatchAt(i, pattern)) return i;
            }
        }
        else
        {
            for (long i = Math.Min(startOffset, _data.Length - pattern.Length); i >= 0; i--)
            {
                if (MatchAt(i, pattern)) return i;
            }
        }
        return -1;
    }

    public long FindText(string text, Encoding encoding, long startOffset)
    {
        var pattern = encoding.GetBytes(text);
        return FindBytes(pattern, startOffset, true);
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Undo()
    {
        if (!CanUndo) return;
        var entry = _undoStack.Pop();

        switch (entry.Kind)
        {
            case UndoKind.Set:
                _data[entry.Offset] = entry.OldSingle!.Value;
                break;
            case UndoKind.SetRange:
                Array.Copy(entry.OldData!, 0, _data, entry.Offset, entry.OldData!.Length);
                break;
            case UndoKind.Insert:
                int insertLen = entry.InsertedData!.Length;
                var afterInsert = new byte[_data.Length - insertLen];
                Array.Copy(_data, 0, afterInsert, 0, entry.Offset);
                Array.Copy(_data, entry.Offset + insertLen, afterInsert, entry.Offset, _data.Length - entry.Offset - insertLen);
                _data = afterInsert;
                OnPropertyChanged(nameof(Length));
                break;
            case UndoKind.Delete:
                var afterUndoDel = new byte[_data.Length + entry.OldData!.Length];
                Array.Copy(_data, 0, afterUndoDel, 0, entry.Offset);
                Array.Copy(entry.OldData, 0, afterUndoDel, entry.Offset, entry.OldData.Length);
                Array.Copy(_data, entry.Offset, afterUndoDel, entry.Offset + entry.OldData.Length, _data.Length - entry.Offset);
                _data = afterUndoDel;
                OnPropertyChanged(nameof(Length));
                break;
        }

        _redoStack.Push(entry);
        MarkModified();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var entry = _redoStack.Pop();

        switch (entry.Kind)
        {
            case UndoKind.Set:
                _data[entry.Offset] = entry.NewSingle!.Value;
                break;
            case UndoKind.SetRange:
                Array.Copy(entry.NewData!, 0, _data, entry.Offset, entry.NewData!.Length);
                break;
            case UndoKind.Insert:
                var afterInsert = new byte[_data.Length + entry.InsertedData!.Length];
                Array.Copy(_data, 0, afterInsert, 0, entry.Offset);
                Array.Copy(entry.InsertedData, 0, afterInsert, entry.Offset, entry.InsertedData.Length);
                Array.Copy(_data, entry.Offset, afterInsert, entry.Offset + entry.InsertedData.Length, _data.Length - entry.Offset);
                _data = afterInsert;
                OnPropertyChanged(nameof(Length));
                break;
            case UndoKind.Delete:
                int delLen = entry.OldData!.Length;
                var afterDel = new byte[_data.Length - delLen];
                Array.Copy(_data, 0, afterDel, 0, entry.Offset);
                Array.Copy(_data, entry.Offset + delLen, afterDel, entry.Offset, _data.Length - entry.Offset - delLen);
                _data = afterDel;
                OnPropertyChanged(nameof(Length));
                break;
        }

        _undoStack.Push(entry);
        MarkModified();
    }

    private bool MatchAt(long offset, byte[] pattern)
    {
        for (int j = 0; j < pattern.Length; j++)
        {
            if (_data[offset + j] != pattern[j]) return false;
        }
        return true;
    }

    private void PushUndo(UndoEntry entry)
    {
        _undoStack.Push(entry);
        _redoStack.Clear();
    }

    private void MarkModified()
    {
        IsModified = true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private enum UndoKind { Set, SetRange, Insert, Delete }

    private class UndoEntry
    {
        public long Offset;
        public UndoKind Kind;
        public byte? OldSingle;
        public byte? NewSingle;
        public byte[]? OldData;
        public byte[]? NewData;
        public byte[]? InsertedData;

        public UndoEntry(long offset, byte oldVal, byte newVal, UndoKind kind)
        {
            Offset = offset;
            Kind = kind;
            OldSingle = oldVal;
            NewSingle = newVal;
        }

        public UndoEntry(long offset, byte[] oldData, byte[]? newData, UndoKind kind)
        {
            Offset = offset;
            Kind = kind;
            OldData = oldData;
            NewData = newData;
            if (kind == UndoKind.Insert) InsertedData = oldData;
        }
    }
}

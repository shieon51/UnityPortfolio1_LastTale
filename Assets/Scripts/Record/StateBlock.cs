using System.Collections.Generic;

// ★ 기록 시스템 2단계 — IRecordable 하나의 상태 덩어리 (기록시스템_설계 6-2)
//   직렬화(4단계)를 고려해 기본형만 담는다: 정수 / 실수 / 정수 사전 / 문자열 목록
public class StateBlock
{
    public string recordId;
    public RecordLayer layer;
    public int version;

    public Dictionary<string, int> ints = new();
    public Dictionary<string, float> floats = new();   // ★ 2-C-2 — 위치처럼 정수로 담을 수 없는 값
    public Dictionary<string, Dictionary<string, int>> intMaps = new();
    public Dictionary<string, List<string>> stringLists = new();
}

// ★ 쓰는 쪽. 넘겨받은 컬렉션은 복사해 담는다 — 원본이 나중에 바뀌어도 스냅샷은 그대로여야 한다
public class StateWriter
{
    private readonly StateBlock _block;
    public StateWriter(StateBlock block) { _block = block; }

    public void WriteInt(string key, int value) => _block.ints[key] = value;
    public void WriteFloat(string key, float value) => _block.floats[key] = value;   // ★ 2-C-2

    public void WriteIntMap(string key, IReadOnlyDictionary<string, int> map)
        => _block.intMaps[key] = map != null ? new Dictionary<string, int>(map) : new Dictionary<string, int>();

    public void WriteStringList(string key, IEnumerable<string> list)
        => _block.stringLists[key] = list != null ? new List<string>(list) : new List<string>();
}

// ★ 읽는 쪽. 복사본을 돌려준다 — 복원한 시스템이 값을 바꿔도 스냅샷이 오염되지 않는다
public class StateReader
{
    private readonly StateBlock _block;
    public StateReader(StateBlock block) { _block = block; }

    public bool Has(string key)
        => _block.ints.ContainsKey(key) || _block.floats.ContainsKey(key)
        || _block.intMaps.ContainsKey(key) || _block.stringLists.ContainsKey(key);

    public int ReadInt(string key, int fallback = 0)
        => _block.ints.TryGetValue(key, out var v) ? v : fallback;

    public float ReadFloat(string key, float fallback = 0f)   // ★ 2-C-2
        => _block.floats.TryGetValue(key, out var v) ? v : fallback;

    public Dictionary<string, int> ReadIntMap(string key)
        => _block.intMaps.TryGetValue(key, out var m) ? new Dictionary<string, int>(m) : new Dictionary<string, int>();

    public List<string> ReadStringList(string key)
        => _block.stringLists.TryGetValue(key, out var l) ? new List<string>(l) : new List<string>();
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Lưu / đọc data game bằng file JSON. Mỗi "bảng" = 1 file: {tableName}.json
///
/// Thứ tự đọc của LoadData:
///   1. persistentDataPath/Data/{table}.json   (file đã lưu của người chơi)
///   2. Resources/Data/{table}.json            (data gốc từ file thiết kế, dùng làm mặc định)
///
/// Chỉ cần đánh [Serializable] cho class, biến public nào có thì lưu biến đó.
/// Lưu ý (giới hạn của JsonUtility): không lưu được Dictionary, và root phải là class
/// (muốn lưu list thì bọc trong class, ví dụ ItemTable { public List&lt;ItemData&gt; items; }).
/// </summary>
public static class DataManager
{
    const string SaveFolder = "Data";   // persistentDataPath/Data
    const string SeedFolder = "Data";   // Assets/Resources/Data

    static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

    static string DirPath => Path.Combine(Application.persistentDataPath, SaveFolder);
    static string FilePath(string table) => Path.Combine(DirPath, table + ".json");

    public static bool HasSavedGame()
    {
        return File.Exists(FilePath(Tables.Player));
    }

    /// <summary>Đọc bảng từ file vào bộ nhớ. Trả về false nếu bảng chưa tồn tại.</summary>
    public static bool LoadData(string table)
    {
        if (string.IsNullOrEmpty(table))
        {
            Debug.LogError("[DataManager] Tên bảng không được để trống.");
            return false;
        }

        try
        {
            string path = FilePath(table);
            if (File.Exists(path))
            {
                cache[table] = File.ReadAllText(path);
                return true;
            }

            TextAsset seed = Resources.Load<TextAsset>(SeedFolder + "/" + table);
            if (seed != null)
            {
                cache[table] = seed.text;
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[DataManager] LoadData(" + table + ") lỗi: " + e.Message);
        }

        cache.Remove(table);
        return false;
    }

    /// <summary>Ghi data vào bảng (cập nhật bộ nhớ + ghi file ngay).</summary>
    public static void SetData<T>(string table, T data)
    {
        if (string.IsNullOrEmpty(table) || data == null)
        {
            Debug.LogError("[DataManager] SetData cần tên bảng và data khác null.");
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(data, true);
            cache[table] = json;

            Directory.CreateDirectory(DirPath);
            string path = FilePath(table);
            string tmp = path + ".tmp";          // ghi file tạm rồi thay thế -> tránh hỏng file khi crash
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception e)
        {
            Debug.LogError("[DataManager] SetData(" + table + ") lỗi: " + e.Message);
        }
    }

    /// <summary>Lấy data của bảng. Chưa có bảng thì trả về object mới (giá trị mặc định).</summary>
    public static T GetData<T>(string table) where T : new()
    {
        if (!cache.ContainsKey(table) && !LoadData(table))
            return new T();

        try
        {
            T result = JsonUtility.FromJson<T>(cache[table]);
            return result != null ? result : new T();
        }
        catch (Exception e)
        {
            Debug.LogError("[DataManager] GetData(" + table + ") lỗi: " + e.Message);
            return new T();
        }
    }

    /// <summary>Xoá file đã lưu của bảng (về lại data gốc trong Resources). Dùng cho New Game.</summary>
    public static void DeleteData(string table)
    {
        cache.Remove(table);
        try
        {
            string path = FilePath(table);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogError("[DataManager] DeleteData(" + table + ") lỗi: " + e.Message);
        }
    }
}

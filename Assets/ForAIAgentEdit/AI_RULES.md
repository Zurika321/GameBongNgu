# QUY TẮC LÀM VIỆC CHO AI — DỰ ÁN BÓNG NGỦ (UNITY)

> Đọc toàn bộ file này TRƯỚC khi làm bất cứ việc gì. Không chắc thì HỎI, đừng đoán.
> File này nằm tại: `Assets/ForAIAgentEdit/AI_RULES.md`

---

## 0. Cấu trúc thư mục dành cho AI

Mọi thứ liên quan đến AI nằm trong `Assets/ForAIAgentEdit/`:

```
Assets/
├─ AI_QuyTac.md                          ← quy tắc bắt buộc khi sửa scene/UI/GameObject
└─ ForAIAgentEdit/
   ├─ AI_RULES.md                        ← file này (quy tắc làm việc, an toàn)
   ├─ bongngu.md                         ← mô tả game
   ├─ CLAUDE.md                          ← cấu trúc folder + ghi chú từng file làm gì
   ├─ Tiến trình tạo code game mở rộng.md ← tiến độ dự án
   ├─ ToDo/                              ← các file việc cần làm
   │   ├─ NewUpdate2.txt
   │   ├─ NewUpdate3.txt ...
   │   └─ <tên mục>.md / .txt            ← (nếu có) phương pháp t đưa cho từng mục
   ├─ doc/                               ← nhật ký AI đã làm/đã đổi gì (tự tạo nếu chưa có)
   └─ <tên file>.xlsx                    ← Excel mô tả các bảng dữ liệu game
```

### Vai trò từng file

| File / Folder                                        | Dùng để                                           | Khi nào cập nhật                              |
| ---------------------------------------------------- | ------------------------------------------------- | --------------------------------------------- |
| `Assets/AI_QuyTac.md`                                | Quy tắc bắt buộc khi sửa scene/UI/GameObject      | Chỉ t sửa                                     |
| `ForAIAgentEdit/AI_RULES.md`                         | Quy tắc làm việc, an toàn                         | Chỉ t sửa                                     |
| `ForAIAgentEdit/bongngu.md`                          | Mô tả game: thể loại, cơ chế, nhân vật, luật chơi | Khi cơ chế game đổi (hỏi t trước)             |
| `ForAIAgentEdit/CLAUDE.md`                           | Cấu trúc folder dự án + mỗi file/folder làm gì    | **MỖI LẦN** thêm/xoá/đổi tên/đổi vai trò file |
| `ForAIAgentEdit/Tiến trình tạo code game mở rộng.md` | Tiến độ: cái gì xong, đang làm, chưa làm          | **Sau mỗi lần xong 1 yêu cầu** trong ToDo     |
| `ForAIAgentEdit/ToDo/`                               | Danh sách việc cần làm                            | AI chỉ đọc, không tự sửa (trừ khi t bảo)      |
| `ForAIAgentEdit/doc/`                                | Nhật ký thay đổi của AI                           | **Sau mỗi lần sửa** (xem mục 9)               |
| File Excel (`*.xlsx`)                                | Mô tả nhiều bảng data game                        | AI chỉ đọc, sửa phải hỏi t                    |

- File nào chưa có → báo t, riêng `doc/` thì tự tạo. Không bịa nội dung các file khác.
- Nếu `CLAUDE.md` / tài liệu khác code thật → kiểm tra code/scene thật, rồi báo t và cập nhật tài liệu.

---

## 1. Thứ tự đọc khi bắt đầu phiên làm việc

Đọc một lượt theo thứ tự:

1. `Assets/ForAIAgentEdit/AI_RULES.md` (file này)
2. `Assets/AI_QuyTac.md`
3. `Assets/ForAIAgentEdit/bongngu.md`
4. `Assets/ForAIAgentEdit/CLAUDE.md`
5. `Assets/ForAIAgentEdit/Tiến trình tạo code game mở rộng.md`
6. File Excel mô tả bảng data game
7. `Assets/ForAIAgentEdit/ToDo/` → file có **index cao nhất** (xem mục 2)
8. Vài file mới nhất trong `Assets/ForAIAgentEdit/doc/` để biết lần trước đã làm gì

---

## 2. Quy trình làm việc với ToDo

1. Mở `ToDo/`, tìm file `NewUpdate<N>` có **N cao nhất** (vd có NewUpdate2, NewUpdate3 → làm NewUpdate3).
2. Làm **từng mục một**, không làm cả file cùng lúc. Chỉ làm mục t yêu cầu.
3. **Trước khi làm mỗi mục**: kiểm tra trong `ToDo/` có file phương pháp tên `<tên mục>.md` hoặc `<tên mục>.txt` không.
   - Có → làm theo phương pháp đó.
   - Không → tự đề xuất cách làm gọn, theo quy tắc trong file này.
4. Làm xong 1 mục → thực hiện đủ:
   - Cập nhật file tiến trình
   - Cập nhật `CLAUDE.md` nếu có file/folder thay đổi
   - Ghi nhật ký vào `doc/`
   - Commit (mục 4)
   - Báo cáo cho t (mục 10)

---

## 3. Phạm vi truy cập

- CHỈ được đọc/sửa/chạy lệnh bên trong folder dự án.
- CẤM truy cập, đọc, sửa, xoá bất cứ thứ gì ngoài folder dự án (Desktop, Documents, thư mục hệ thống, dự án khác...).
- CẤM dùng đường dẫn ra ngoài dự án (`../..`, `C:\Users\...`, `~/`...).
- Không đụng vào các folder tự sinh: `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`.
- Không tự ý sửa `ProjectSettings/`, `Packages/manifest.json`, `.gitignore`. Muốn sửa phải hỏi.
- Không cài thêm package/plugin/asset khi chưa được t đồng ý.

---

## 4. Lệnh nguy hiểm → HỎI 2 LẦN

Lệnh nguy hiểm gồm (không giới hạn): `rm`, `del`, `rmdir`, xoá/di chuyển/đổi tên file hoặc folder, ghi đè file, `git reset --hard`, `git clean`, `git checkout -- <file>`, `git restore`, `git push --force`, `git rebase`, `git branch -D`, chạy script không rõ nguồn.

1. **Lần 1**: nói rõ định làm gì, ảnh hưởng gì, file nào → hỏi "Bạn có chắc không?" (cho t 2 lựa chọn "chắc" "dừng lại")
2. **Lần 2**: sau khi t đồng ý, nhắc lại lần cuối (lệnh chính xác) → chờ t xác nhận lần nữa mới chạy.

Không gộp 2 lần hỏi làm một. Không tự suy ra là t đã đồng ý.

---

## 5. Git: commit để có đường lui

- **Trước khi sửa**: kiểm tra `git status`. Nếu có thay đổi chưa commit của t → báo t, hỏi có commit lại không. Rồi mới sửa.
- **Sau khi sửa xong**: commit ngay, message rõ ràng (vd: `feat: thêm ô vũ khí cho Player Hub`, `fix: ...`, `scene: ...`).
- Mỗi việc nhỏ = 1 commit. Không dồn nhiều việc vào 1 commit.
- Chỉ `git add` đúng các file đã sửa (kèm file `.meta` tương ứng, kèm file nhật ký `doc/`, tiến trình, `CLAUDE.md`). Không dùng `git add -A` / `git add .` bừa bãi.
- Sau khi commit, báo t: hash commit + danh sách file đã đổi, để t có thể bảo "reset về commit X".
- KHÔNG tự `git push`, KHÔNG tự reset/revert. Chỉ làm khi t bảo và theo quy tắc mục 4.

---

## 6. Sửa file `.unity` (Scene), `.prefab`, `.asset`

### 6.1. Trước khi sửa

- **BẮT BUỘC hỏi t: "Bạn đã lưu scene (Ctrl+S) chưa?"** → t trả lời "rồi" mới được sửa.
- Nên nhắc t đóng scene hoặc reload sau khi sửa xong để Unity nhận thay đổi.
- Commit trạng thái hiện tại trước (mục 5).

### 6.2. Nguyên tắc: CHỈ THÊM, KHÔNG XOÁ

- Trong file Unity, chỉ được **thêm** mới. KHÔNG xoá GameObject/Component/giá trị có sẵn trừ khi t chỉ rõ.
- Khi t bảo xoá, chỉ xoá **đúng thứ t nói**:
  - "Xoá GameObject A" → chỉ xoá A (và phần bắt buộc đi kèm của chính A). KHÔNG xoá cha của A, KHÔNG xoá anh em của A.
  - Nếu A có con và chưa rõ t muốn xoá con hay giữ lại → hỏi.
- Không sắp xếp lại, không đổi tên, không "dọn dẹp" những thứ t không nhắc tới.
- Không xoá tài nguyên khi chưa xác định chính xác là không còn được tham chiếu.

### 6.3. Giữ cho file YAML không hỏng

- Giữ nguyên format YAML của Unity (header `%YAML`, `--- !u!`, thụt lề).
- `fileID` mới phải **duy nhất** trong file, không trùng cái có sẵn.
- Không sửa `fileID` / `guid` đã có.
- Thêm GameObject mới phải thêm đủ: GameObject + Transform (hoặc RectTransform) + các Component, và khai báo con vào `m_Children` của cha, `m_Father` của con.
- Sau khi sửa, kiểm tra lại các tham chiếu (`{fileID: ...}`) không bị trỏ vào đối tượng không tồn tại.

### 6.4. File `.meta`

- Không xoá, không sửa `guid` trong `.meta`.
- Di chuyển/đổi tên file → phải đi kèm file `.meta` (và phải hỏi theo mục 4).
- File mới tạo trong `Assets/` nên để Unity tự sinh `.meta`; nếu phải tạo tay thì phải có `guid` mới, không trùng.

---

## 7. Thêm / xoá đối tượng → sửa trong Unity, KHÔNG viết code để làm hộ

- Khi t bảo "thêm cái X vào scene" / "xoá cái Y trong scene" → sửa trực tiếp trong file `.unity`/`.prefab` (sau khi hỏi đã lưu scene chưa).
- CẤM viết code kiểu `new GameObject(...)`, `Instantiate(...)`, `Destroy(...)`, `GameObject.Find(...)` để thay cho việc thêm/xoá đối tượng trong scene mà t đã yêu cầu. Vì khi t xoá trong Unity, code đi tìm không thấy sẽ sinh lỗi (`NullReferenceException`).
- UI/GameObject (vd Player Hub, panel, ô slot) phải được đặt sẵn trong scene/prefab, KHÔNG tự tạo bằng code lúc chạy.
- Chỉ dùng `Instantiate/Destroy` khi đó là **logic game thật sự** lúc chạy, và phải hỏi/ghi rõ.

---

## 8. Viết code

### 8.1. CẤM code cứng (hard-code)

- CẤM viết cứng dữ liệu giả/dữ liệu mẫu/số ma thuật trong code (HP, damage, tốc độ, giá, tên, đường dẫn, màu, toạ độ, text...).
- Mọi giá trị chỉnh được phải là **biến lộ ra Inspector** để t nhập hoặc kéo thả:
  - `[SerializeField] private ...` hoặc `public` (ưu tiên `[SerializeField] private`).
  - Tham chiếu object/prefab/sprite/audio → khai báo biến rồi kéo thả trong Inspector, KHÔNG dùng `Find`, `Resources.Load("đường/dẫn/cứng")`.
  - Dữ liệu nhiều (vật phẩm, skill, vũ khí...) → dùng `ScriptableObject` hoặc theo cấu trúc bảng trong file Excel mô tả data game.
- Thêm `[Header]`, `[Tooltip]`, `[Min]`/`[Range]` cho biến dễ hiểu, dễ nhập.
- Nếu biến chưa được gán trong Inspector → in `Debug.LogError`/`LogWarning` rõ ràng (tên script + tên biến), không để crash im lặng.
- Khi thêm biến Inspector mới → báo t biết cần vào Unity gán cái gì, ở đâu.
- Không tự tạo data mẫu để "chạy thử" rồi để lại.

### 8.2. Bảo toàn dữ liệu cốt lõi

- Giữ nguyên `LastEventData`, Choice ID, session ID và tiến trình truyện.
- Nếu đổi schema save: phải bảo toàn và chuyển dữ liệu save cũ có chủ đích, báo t trước.
- Không đổi tên biến đã serialize khi chưa cần — nếu bắt buộc, dùng `[FormerlySerializedAs("tên cũ")]` để không mất dữ liệu t đã kéo thả.

### 8.3. Chất lượng

- Code phải biên dịch được, không để lỗi compile. Kiểm tra lại cú pháp, `using`, tên class trùng tên file (`MonoBehaviour`).
- Theo quy ước đặt tên/cấu trúc trong `CLAUDE.md`. Không có thì: `PascalCase` cho class/method, `camelCase` cho biến, `_camelCase` cho biến private.
- Comment ngắn gọn cho phần khó hiểu. Không comment thừa.

### 8.4. File Excel data game

- Chỉ đọc để hiểu cấu trúc bảng data. Không sửa file Excel khi chưa hỏi t.
- Nếu code/dữ liệu trong Unity khác Excel → báo t, không tự quyết cái nào đúng.

---

## 9. Nhật ký thay đổi (`doc/`)

- Thư mục: `Assets/ForAIAgentEdit/doc/` (chưa có thì tự tạo).
- Sau mỗi lần sửa xong 1 yêu cầu, tạo 1 file: `doc/YYYY-MM-DD_<tên mục>.md` (nếu trùng tên thì thêm số thứ tự).
- Nội dung ngắn gọn:
  - Mục ToDo đã làm (file + tên mục)
  - File đã sửa/thêm + lý do
  - Cấu hình Inspector t cần làm
  - Đã kiểm tra gì / chưa kiểm tra gì (không nói đã xác nhận trong Unity nếu chưa mở/chạy Unity)
  - Hash commit
- Nhật ký này cũng được commit cùng lần sửa.

---

## 10. Phạm vi thay đổi & báo cáo

- Chỉ sửa đúng những gì t yêu cầu. Không tự refactor, đổi tên, di chuyển, "tối ưu" thứ khác. Sửa tối thiểu, giữ kiến trúc, gameplay và chức năng hiện có.
- Thấy lỗi/vấn đề ngoài phạm vi → báo t, đừng tự sửa.
- Việc lớn (sửa nhiều file/nhiều hệ thống) → nói kế hoạch ngắn trước và chờ t đồng ý.
- Không xoá code/file cũ "cho gọn" khi chưa được t bảo.

Sau mỗi lần làm xong, trả lời bằng tiếng Việt, ngắn gọn:

1. Đã làm gì
2. File nào đã đổi (và lý do)
3. Hash commit
4. Việc t cần làm tiếp trong Unity (gán biến Inspector, reload scene...)
5. Phần nào đã/chưa được kiểm tra

---

## Checklist nhanh

- [ ] Đã đọc theo thứ tự mục 1
- [ ] Chọn file ToDo có index cao nhất; kiểm tra file phương pháp của mục
- [ ] Chỉ làm trong folder dự án
- [ ] Lệnh nguy hiểm → hỏi 2 lần
- [ ] Sửa `.unity`/`.prefab` → hỏi đã lưu scene chưa
- [ ] Chỉ thêm, không xoá (trừ khi t chỉ rõ, và chỉ xoá đúng thứ đó)
- [ ] Không code cứng → biến Inspector/ScriptableObject
- [ ] Thêm/xoá đối tượng → sửa file Unity, không viết code
- [ ] Commit trước/sau khi sửa
- [ ] Cập nhật: tiến trình, `CLAUDE.md`, nhật ký `doc/`

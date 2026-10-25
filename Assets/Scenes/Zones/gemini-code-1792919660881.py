import os
import re
import shutil

def replace_in_block(content, object_name, replacements):
    """Tìm khối YAML chứa m_Name: object_name và cập nhật các thông số trong khối đó hoặc các Component thuộc về cùng GameObject."""
    # Tìm GameObject có m_Name tương ứng để lấy danh sách component fileID
    go_pattern = re.compile(
        r"--- !u!1 &(\d+)\r?\nGameObject:.*?m_Component:\r?\n((?:  - component: \{fileID: \d+\}\r?\n)+).*?m_Name: "
        + re.escape(object_name)
        + r"\r?\n",
        re.DOTALL,
    )

    match = go_pattern.search(content)
    if not match:
        print(f"  [!] Không tìm thấy đối tượng: {object_name}")
        return content

    comp_section = match.group(2)
    comp_ids = re.findall(r"fileID: (\d+)", comp_section)

    # Duyệt qua từng component thuộc về GameObject này và áp dụng thay thế
    for cid in comp_ids:
        block_pattern = re.compile(
            r"(--- !u!\d+ &" + cid + r"\r?\n.*?)(\r?\n--- !u!|\Z)", re.DOTALL
        )
        block_match = block_pattern.search(content)
        if block_match:
            block_text = block_match.group(1)
            new_block_text = block_text
            for old_str, new_str in replacements:
                new_block_text = re.sub(old_str, new_str, new_block_text)
            if new_block_text != block_text:
                content = (
                    content[: block_match.start(1)]
                    + new_block_text
                    + content[block_match.end(1) :]
                )

    return content


def upgrade_core_combat_demo(filepath="CoreCombatDemo.unity"):
    if not os.path.exists(filepath):
        print(f"Không tìm thấy file {filepath}")
        return

    # Tạo bản sao lưu an toàn
    backup_path = filepath + ".bak"
    shutil.copyfile(filepath, backup_path)
    print(f"Đã tạo bản backup tại: {backup_path}")

    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    # 1. Nâng cấp màu sắc & Bố cục từng thành phần UI
    ui_updates = {
        # Camera Background: Đen sâu thẳm
        "Main Camera": [
            (
                r"m_BackGroundColor: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_BackGroundColor: {r: 0.05, g: 0.05, b: 0.08, a: 1}",
            )
        ],
        # Thanh máu & thanh Stun của Player nhìn sắc nét, đậm chất RPG hơn
        "Player": [
            (
                r"healthBarBackgroundColor: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "healthBarBackgroundColor: {r: 0.06, g: 0.04, b: 0.06, a: 0.9}",
            ),
            (
                r"healthBarFillColor: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "healthBarFillColor: {r: 0.82, g: 0.18, b: 0.22, a: 1}",
            ),
            (
                r"stunBarFillColor: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "stunBarFillColor: {r: 0.92, g: 0.72, b: 0.28, a: 1}",
            ),
        ],
        # Lớp phủ khi Pause Game: Đen mờ sâu hơn
        "Paused Menu Overlay": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.02, g: 0.02, b: 0.04, a: 0.88}",
            )
        ],
        # Cửa sổ Menu chính
        "Menu Window": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.08, g: 0.07, b: 0.11, a: 0.98}",
            )
        ],
        "Menu Title": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.95, g: 0.82, b: 0.52, a: 1}",
            ),
            (r"m_FontStyle: 0", "m_FontStyle: 1"),  # In đậm tiêu đề
        ],
        # Nút mở/đóng Menu
        "Open Menu": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.14, g: 0.12, b: 0.18, a: 0.95}",
            ),
            (
                r"m_AnchoredPosition: \{x: 1110, y: -20\}",
                "m_AnchoredPosition: {x: 1120, y: -16}",
            ),
            (
                r"m_SizeDelta: \{x: 150, y: 54\}",
                "m_SizeDelta: {x: 140, y: 44}",
            ),
        ],
        "Close Menu": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.48, g: 0.14, b: 0.16, a: 1}",
            )
        ],
        # 3 Tab chuyển đổi: Căn đều khoảng cách và đổi sang màu trầm cổ điển
        "Inventory Tab": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.18, g: 0.15, b: 0.22, a: 1}",
            ),
            (
                r"m_AnchoredPosition: \{x: 157, y: -78\}",
                "m_AnchoredPosition: {x: 140, y: -76}",
            ),
            (
                r"m_SizeDelta: \{x: 175, y: 44\}",
                "m_SizeDelta: {x: 190, y: 42}",
            ),
        ],
        "Map Tab": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.18, g: 0.15, b: 0.22, a: 1}",
            ),
            (
                r"m_AnchoredPosition: \{x: 428, y: -78\}",
                "m_AnchoredPosition: {x: 345, y: -76}",
            ),
            (
                r"m_SizeDelta: \{x: 175, y: 44\}",
                "m_SizeDelta: {x: 190, y: 42}",
            ),
        ],
        "Settings Tab": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.18, g: 0.15, b: 0.22, a: 1}",
            ),
            (
                r"m_AnchoredPosition: \{x: 694, y: -78\}",
                "m_AnchoredPosition: {x: 550, y: -76}",
            ),
            (
                r"m_SizeDelta: \{x: 175, y: 44\}",
                "m_SizeDelta: {x: 190, y: 42}",
            ),
        ],
        # Giao diện Túi Đồ (Inventory)
        "Inventory Content": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.06, g: 0.05, b: 0.08, a: 1}",
            ),
            (
                r"emptySlotColor: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "emptySlotColor: {r: 0.10, g: 0.09, b: 0.13, a: 1}",
            ),
            (
                r"itemSlotColor: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "itemSlotColor: {r: 0.19, g: 0.16, b: 0.22, a: 1}",
            ),
            (
                r"selectedSlotColor: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "selectedSlotColor: {r: 0.78, g: 0.55, b: 0.22, a: 1}",
            ),
            (
                r"slotOutlineColor: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "slotOutlineColor: {r: 0.45, g: 0.36, b: 0.25, a: 1}",
            ),
        ],
        "Inventory Heading": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.92, g: 0.78, b: 0.48, a: 1}",
            )
        ],
        "Item Details": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.11, g: 0.10, b: 0.15, a: 1}",
            )
        ],
        # Sửa lỗi tràn lề của 3 nút chức năng trong Item Details
        "Equip Item": [
            (
                r"m_AnchoredPosition: \{x: 18, y: 15\}",
                "m_AnchoredPosition: {x: 88, y: 28}",
            ),
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.18, g: 0.38, b: 0.28, a: 1}",
            ),
        ],
        "Use Item": [
            (
                r"m_AnchoredPosition: \{x: -18, y: 15\}",
                "m_AnchoredPosition: {x: -88, y: 28}",
            ),
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.48, g: 0.26, b: 0.16, a: 1}",
            ),
        ],
        "Read Book": [
            (
                r"m_AnchoredPosition: \{x: 18, y: 68\}",
                "m_AnchoredPosition: {x: 88, y: 78}",
            ),
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.24, g: 0.32, b: 0.44, a: 1}",
            ),
        ],
        "Equipped Weapon": [
            (
                r"m_AnchoredPosition: \{x: 18, y: 48\}",
                "m_AnchoredPosition: {x: 18, y: 115}",
            )
        ],
        # Giao diện Settings & Nút Main Menu
        "Settings Content": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.06, g: 0.05, b: 0.08, a: 1}",
            )
        ],
        "Settings Heading": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.92, g: 0.78, b: 0.48, a: 1}",
            )
        ],
        "Fill": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.78, g: 0.55, b: 0.22, a: 1}",
            )
        ],
        "Button": [  # Nút Main Menu trong Settings
            (
                r"m_Color: \{r: 1, g: 1, b: 1, a: 1\}",
                "m_Color: {r: 0.42, g: 0.14, b: 0.16, a: 1}",
            ),
            (
                r"m_SizeDelta: \{x: 160, y: 30\}",
                "m_SizeDelta: {x: 200, y: 42}",
            ),
        ],
        # Minimap góc trái trên
        "Map Content": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.05, g: 0.05, b: 0.08, a: 0.92}",
            ),
            (
                r"m_AnchoredPosition: \{x: 10, y: -10\}",
                "m_AnchoredPosition: {x: 16, y: -16}",
            ),
        ],
        "Map Viewport": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.08, g: 0.09, b: 0.12, a: 1}",
            )
        ],
        # Prompt tương tác "Press E to interact"
        "NPC Interaction Prompt": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.06, g: 0.05, b: 0.09, a: 0.94}",
            ),
            (
                r"m_AnchoredPosition: \{x: 0, y: 42\}",
                "m_AnchoredPosition: {x: 0, y: 48}",
            ),
            (
                r"m_SizeDelta: \{x: 360, y: 48\}",
                "m_SizeDelta: {x: 340, y: 44}",
            ),
        ],
        "Prompt Text": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.95, g: 0.85, b: 0.60, a: 1}",
            )
        ],
        # Khung hội thoại NPC (NPC Dialogue)
        "NPC Dialogue": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.18, g: 0.14, b: 0.18, a: 0.98}",
            ),
            (
                r"m_AnchoredPosition: \{x: 0, y: 148\}",
                "m_AnchoredPosition: {x: 0, y: 152}",
            ),
            (
                r"m_SizeDelta: \{x: 920, y: 260\}",
                "m_SizeDelta: {x: 960, y: 250}",
            ),
        ],
        "Portrait Frame": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.10, g: 0.08, b: 0.13, a: 1}",
            )
        ],
        "Dialogue Box": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.06, g: 0.05, b: 0.08, a: 1}",
            )
        ],
        "Speaker": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.96, g: 0.78, b: 0.42, a: 1}",
            ),
            (r"m_FontStyle: 0", "m_FontStyle: 1"),
        ],
        "Dialogue Text": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.92, g: 0.90, b: 0.86, a: 1}",
            ),
            (r"m_LineSpacing: 1", "m_LineSpacing: 1.15"),
        ],
        # Sửa lỗi vị trí bị bay lên trời của cụm nút lựa chọn hội thoại
        "Dialogue Choices": [
            (
                r"m_AnchoredPosition: \{x: 383, y: 311\}",
                "m_AnchoredPosition: {x: 12, y: 8}",
            ),
            (
                r"m_AnchorMax: \{x: 0.68, y: 0.34\}",
                "m_AnchorMax: {x: 0.66, y: 0.45}",
            ),
        ],
        "Choice 1": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.16, g: 0.13, b: 0.20, a: 1}",
            )
        ],
        "Choice 2": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.16, g: 0.13, b: 0.20, a: 1}",
            )
        ],
        "Choice 3": [
            (
                r"m_Color: \{r: [0-9.]+, g: [0-9.]+, b: [0-9.]+, a: [0-9.]+\}",
                "m_Color: {r: 0.16, g: 0.13, b: 0.20, a: 1}",
            )
        ],
    }

    for obj_name, rules in ui_updates.items():
        content = replace_in_block(content, obj_name, rules)

    # Đổi màu chữ "Main Menu" trong Settings từ xám đen sang trắng kem để nổi trên nền đỏ
    content = content.replace(
        "m_fontColor: {r: 0.19607843, g: 0.19607843, b: 0.19607843, a: 1}",
        "m_fontColor: {r: 0.96, g: 0.92, b: 0.84, a: 1}",
    )

    # 2. Đồng bộ hiệu ứng chuyển màu (Hover / Press) cho toàn bộ Button trong Scene
    content = content.replace(
        "m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}",
        "m_HighlightedColor: {r: 1, g: 0.88, b: 0.68, a: 1}",
    )
    content = content.replace(
        "m_PressedColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}",
        "m_PressedColor: {r: 0.72, g: 0.52, b: 0.38, a: 1}",
    )

    with open(filepath, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"-> Đã nâng cấp thành công giao diện trong {filepath}!")


def upgrade_zone1(filepath="Zone1.unity"):
    if not os.path.exists(filepath):
        print(f"Không tìm thấy file {filepath}")
        return

    backup_path = filepath + ".bak"
    shutil.copyfile(filepath, backup_path)
    print(f"Đã tạo bản backup tại: {backup_path}")

    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    # Làm dịu bảng màu các vùng trên Minimap (MapZoneRegion) để hợp với UI mới
    map_color_replacements = [
        # Quảng Trường & Nhà Kho Đá: Đỏ gạch trầm
        (
            "mapColor: {r: 0.72, g: 0.43, b: 0.34, a: 1}",
            "mapColor: {r: 0.52, g: 0.26, b: 0.24, a: 0.9}",
        ),
        # Làng Ven Sông: Xanh rêu tối
        (
            "mapColor: {r: 0.24, g: 0.65, b: 0.4, a: 1}",
            "mapColor: {r: 0.18, g: 0.42, b: 0.32, a: 0.9}",
        ),
        # Nhà Hoang & Gác Mái: Nâu gỗ mục
        (
            "mapColor: {r: 0.7, g: 0.54, b: 0.4, a: 1}",
            "mapColor: {r: 0.46, g: 0.35, b: 0.28, a: 0.9}",
        ),
        # Đồi Cao (Am Thờ): Vàng đồng sẫm
        (
            "mapColor: {r: 0.75, g: 0.58, b: 0.3, a: 1}",
            "mapColor: {r: 0.56, g: 0.42, b: 0.22, a: 0.9}",
        ),
    ]

    # Chỉnh lại màu nền môi trường (Placeholder Ground & Đường đi) sang tông đêm kinh dị
    env_color_replacements = [
        # Nền đất Làng Ven Sông
        (
            "m_Color: {r: 0.23, g: 0.25, b: 0.22, a: 1}",
            "m_Color: {r: 0.12, g: 0.14, b: 0.13, a: 1}",
        ),
        # Nền đất Rừng Thông
        (
            "m_Color: {r: 0.18, g: 0.25, b: 0.2, a: 1}",
            "m_Color: {r: 0.09, g: 0.14, b: 0.11, a: 1}",
        ),
        # Nền đất Đồi Cao Am Thờ
        (
            "m_Color: {r: 0.27, g: 0.24, b: 0.2, a: 1}",
            "m_Color: {r: 0.15, g: 0.13, b: 0.12, a: 1}",
        ),
        # Nền đất Quảng Trường
        (
            "m_Color: {r: 0.3, g: 0.28, b: 0.25, a: 1}",
            "m_Color: {r: 0.16, g: 0.15, b: 0.16, a: 1}",
        ),
        # Dải sông ven rừng: Xanh nước đêm sâu hơn
        (
            "m_Color: {r: 0.14, g: 0.34, b: 0.42, a: 1}",
            "m_Color: {r: 0.08, g: 0.18, b: 0.26, a: 1}",
        ),
    ]

    for old_str, new_str in map_color_replacements + env_color_replacements:
        content = content.replace(old_str, new_str)

    with open(filepath, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"-> Đã nâng cấp thành công bảng màu trong {filepath}!")


if __name__ == "__main__":
    # Tự động lấy đúng đường dẫn thư mục chứa file .py này (Assets/Scenes/Zones)
    base_dir = os.path.dirname(os.path.abspath(__file__))

    upgrade_core_combat_demo(os.path.join(base_dir, "CoreCombatDemo.unity"))
    upgrade_zone1(os.path.join(base_dir, "Zone1.unity"))
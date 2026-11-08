# GroupBlock - xac minh ban cap nhat


KIEM TRA TAI PROJECT THUC TE - 08/11/2026 (bo sung sau bao cao tren)
- Da doc NewUpdate4 va doi chieu: HTML hien tai va ca CharacterAnimation.cs/CharacterAnimationEditor.cs da co GroupBlock.
- Da tao ban sao nguyen noi dung HTML hien tai cung thu muc: BoneAnimation_Unity_GroupBlock_NewUpdate4.html. Khong ap dung ban patch schema cu.
- Schema dang dung: groupBlocks / name / defaultVariant / variants / blockIds; giu schemaVersion 1.
- JavaScript cua HTML moi: node --check PASS.
- C# runtime + Editor cua project thuc te: build Validation/SkillActions/Editor.csproj PASS.
- Chua chay lai browser interaction tests, Unity Inspector/Play Mode; ket qua Chromium trong bao cao truoc la ket qua do nguoi dung cung cap.
- B1 mapping weaponId va B2 Edit Mode live preview van chua thuc hien trong luot nay; khong sua C# hay du lieu gameplay.
- Khong commit theo lua chon da co.

File kiem tra: Validation/GroupBlockWeb/updated-source.json va updated-app.js. Khong sua HTML goc, C#, scene, prefab, Excel hoac JSON animation.

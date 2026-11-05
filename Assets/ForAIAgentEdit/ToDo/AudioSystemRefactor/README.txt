AUDIO SYSTEM - HUONG DAN CAI DAT

CAC FILE
- AudioConfig.cs: ScriptableObject chua danh sach Music va SFX.
- VolumeManager.cs: singleton quan ly phat nhac, SFX, volume va mute.
- FootstepAudioPlayer.cs: tu phat footstep bang SFX ID.
- GameIdRegistry.cs: phien ban bo sung Music ID va SFX ID cho registry hien tai.

1. CAP NHAT RegistryIdType
Trong enum RegistryIdType hien tai, them 2 gia tri:
    Music,
    Sfx

Khong tao enum moi neu project da co RegistryIdType. Neu RegistryIdPropertyDrawer co switch/case rieng,
bo sung Music -> GameIdRegistry.musicIds va Sfx -> GameIdRegistry.sfxIds.
Neu drawer da goi GameIdRegistry.GetList(type), khong can sua them.

2. THAY GameIdRegistry.cs
Thay file GameIdRegistry.cs hien tai bang file trong goi nay. Giu nguyen cac class ItemDefinition,
ItemCategory, RegistryIdType va RegistryId attribute/drawer dang co trong project.

3. TAO AUDIO CONFIG
- Tao asset: Create > Bóng Ngủ > Audio Config.
- Them cac dong trong Musics va Sfx.
- Dang ky ID truoc trong GameIdRegistry:
  + musicIds: menu, exploration, battle...
  + sfxIds: footstep, hit, jump, open_door...
- Sau do chon ID tu dropdown trong AudioConfig va gan AudioClip.

Music fields:
- Volume: am luong rieng cua bai nhac.
- Pitch: toc do/cao do phat.
- Loop: co lap lai hay khong.
- Stop After Seconds: 0 = khong tu dung; > 0 = tu dung sau so giay.
  Neu Loop tat va Stop After Seconds = 0, clip tu ket thuc theo do dai that.

SFX fields:
- Volume va Pitch la thong so mac dinh cua SFX.

4. GAN VOLUME MANAGER
- Dat VolumeManager tren GameObject trong scene khoi dong (vi du: _Systems).
- Gan AudioConfig vao truong Audio Config.
- Khong can dat VolumeManager trong cac scene tiep theo; DontDestroyOnLoad se giu no.
- Xoa VolumeManager cu trong cac scene khac de tranh duplicate.
- Neu muon tu phat nhac khi vao game, bat Play Startup Music va chon Startup Music ID.

Luu y: phien ban nay bo Bootstrap tu tao GameObject, vi object tu tao khong the nhan AudioConfig da gan trong Inspector.
Vi vay can co mot VolumeManager trong scene dau tien.

5. API SU DUNG
    VolumeManager.ToggleMusic("menu", true);       // bat nhac menu
    VolumeManager.ToggleMusic("battle", true);     // doi sang nhac battle
    VolumeManager.ToggleMusic("battle", false);    // tat neu battle dang phat

    VolumeManager.PlaySFX("hit");                  // phat mot lan
    VolumeManager.PlaySFXLoop("fire", 4f);          // lap 4 giay roi tu dung
    VolumeManager.PlaySFXLoop("wind");              // lap den khi stop
    VolumeManager.StopSFXLoop("wind");              // dung vong lap wind

6. FOOTSTEP
- Them ID "footstep" vao sfxIds cua GameIdRegistry.
- Tao dong SFX co ID footstep trong AudioConfig va gan clip buoc chan.
- Tren Player can co Rigidbody2D va tag Player.
- Gan ID vao truong Default Footstep SFX ID trong VolumeManager. VolumeManager tu them FootstepAudioPlayer cho cac object tag Player khi scene duoc load va gan ID nay.
- Neu Player da co FootstepAudioPlayer trong prefab, ID mac dinh tu VolumeManager se duoc ap dung neu da dien; neu de trong, ID da chon tren prefab duoc giu nguyen.
- Neu Player dung Rigidbody3D thay vi Rigidbody2D, can doi phan lay van toc trong FootstepAudioPlayer.

7. VOLUME SETTINGS UI
VolumeSettingsUI hien tai khong can sua; cac ham SetMasterVolume/SetMusicVolume/SetSFXVolume va mute van duoc giu.

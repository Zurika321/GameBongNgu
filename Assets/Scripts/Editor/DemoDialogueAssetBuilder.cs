using UnityEditor;
using UnityEngine;

// Creates editable session dialogue assets from the game's design data.
public static class DemoDialogueAssetBuilder
{
    private const string Root = "Assets/Data/Dialogues";

    private static readonly Entry[] Entries =
    {
        new Entry("1", "Vô Diện", "Khải nhìn vào gương vỡ, mặt phản chiếu bị mất. Giọng thì thầm: ‘Dậy rồi à... Máu trên tay ngươi vẫn còn ấm. Ngươi không muốn biết đêm qua ai đã cầm con dao này sao?’", "Ngươi là ai?", "Vô Diện đáp: ‘Một kẻ chia sẻ giường với ngươi. Nhìn xuống đất đi. Mùi máu đã gọi chúng tới.’", 0, 0, null, null, 0, 0),
        new Entry("1.5", "Khải", "Lũ chuột đã chết. Khải lật xác nhìn: một dân quân trẻ, tay đeo vòng vải của làng, ngực có vết cắt. Anh không nhớ gì.", "Chôn xác, im lặng rời đi", "Khải chôn Tiểu Bảy và rời căn nhà. Mảnh giấy trong túi áo vẫn chưa được đọc.", 0, 1, "Lục soát xác chết", "Trong túi xác có mảnh giấy vẽ cầu thang lên gác mái, ghi: ‘Cậu Bảy, đừng đi một mình.’ Khải nhận ra người chết là Tiểu Bảy.", 1, 0),
        new Entry("2A", "Trưởng Làng Tứ", "Máu trên áo ngươi là của ai? Nói thật, ta sẽ tha.", "Nói dối: đó không phải máu người", "Tứ không tin lời Khải. Ông ra hiệu cho dân quân tránh đường, nhưng vẫn dặn mọi người để mắt đến anh.", 0, 1, "Thú nhận một phần sự thật", "Tứ hạ thấp ngọn đuốc. Ông cảnh báo Khải rằng dân làng sẽ không tha thứ nếu có thêm người bị hại.", 1, 0),
        new Entry("2B", "Khải", "Cuốn nhật ký trên gác mái kể về Lời Nguyền Song Miên. Những dòng cuối bị nhòe bởi máu khô.", "Đọc hết cuốn nhật ký", "Khải ghi nhớ những trang cuối. Các dấu vết trong căn nhà giờ đây có ý nghĩa khác.", 1, 0, "Đốt cuốn nhật ký", "Ngọn lửa nuốt lấy những dòng chữ. Khải không thể biết được phần sự thật đã mất.", 0, 1),
        new Entry("3", "Thầy Hạo", "Ta có thể khóa nó lại mỗi đêm, nhưng cái giá là một mảnh ký ức của con. Con chịu không?", "Nhận bùa khóa quỷ", "Hạo buộc lá bùa vào cổ tay Khải. Một ký ức tuổi thơ nhạt dần, đổi lại bóng tối bị ghìm xuống.", 2, -1, "Từ chối bùa", "Khải từ chối để người khác quyết định mình phải quên điều gì. Hạo lặng lẽ cất lá bùa đi.", 0, 1),
        new Entry("4A", "Vô Diện", "Ngươi nghĩ khóa được ta sao? Ta chỉ đang... nghỉ ngơi.", "Lắng nghe giọng nói", "Vô Diện bật cười khẽ. Trong gương, cái bóng dường như đang chờ Khải hỏi thêm.", 0, 1, "Chống lại nó", "Khải quay mặt khỏi những tấm gương và tiếp tục bước qua hành lang.", 1, 0),
        new Entry("4B", "Én", "Tôi sẽ canh cho tới sáng. Dù có chuyện gì, tôi cũng không mở cửa đâu.", "Tự trói mình trong kho", "Khải tự khóa cửa kho. Tiếng bước chân của Én vẫn đều đặn ở phía bên kia.", 0, 0, "Nhờ Én canh bên ngoài", "Én nhận lời đứng gác. Khải biết quyết định này có thể đặt người bạn của mình vào nguy hiểm.", 1, 0),
        new Entry("5", "Bà Tư Bán Thuốc", "Cậu mà đánh lui được lũ quái đêm nay, cả thị trấn này mang ơn cậu suốt đời.", "Ở lại bảo vệ thị trấn", "Bà Tư trao cho Khải thuốc men còn lại. Cả thị trấn chuẩn bị cho một đêm dài.", 1, 0, "Rời thị trấn trước khi trời tối", "Khải lặng lẽ rời cổng chợ. Tiếng chuông báo động vẫn vang lên sau lưng.", 0, 1),
        new Entry("6A", "Nhi", "Sao mỗi sáng lại có người mất tích ngay sau khi anh ‘chiến đấu’ đêm qua?", "Tự giam mình lại ban đêm", "Nhi nhìn Khải hồi lâu rồi gật đầu. Cô sẽ giúp tìm cách giữ mọi người an toàn.", 2, 0, "Che giấu những gì đã xảy ra", "Khải nói rằng mình không biết gì. Nhi không tranh cãi, nhưng ánh mắt cô đã đổi khác.", 0, 2),
        new Entry("6B", "Bé Tí", "Chị con... chị con biến mất rồi... chú cứu chị con được không?", "Giúp Bé Tí tìm chị", "Khải hứa sẽ tìm dấu vết người chị. Bé Tí đưa anh chiếc bùa nhỏ em vẫn giữ.", 1, 0, "Tiếp tục đi một mình", "Khải bước vào màn sương. Tiếng gọi của đứa trẻ nhỏ dần phía sau.", 0, 1),
        new Entry("7A", "Nhi", "Tôi không sợ anh. Tôi chỉ sợ anh sẽ tự hủy hoại mình trước khi để tôi giúp.", "Kể cho Nhi toàn bộ sự thật", "Nhi lắng nghe mà không ngắt lời. Cô hứa sẽ cùng Khải tìm cách chấm dứt chuyện này.", 2, 0, "Giữ khoảng cách", "Khải đổi chủ đề. Nhi không ép anh nói, nhưng cuộc trò chuyện khép lại sớm.", 0, 0),
        new Entry("7B", "Vô Diện", "Ta không muốn giết ai cả. Ta chỉ muốn được sống. Để ta giúp ngươi, và không ai phải chết oan nữa.", "Chấp nhận hợp đồng", "Khải cho Vô Diện mượn quyền kiểm soát khi nguy cấp. Một dấu ấn tối màu hiện lên trên tay anh.", 0, 2, "Từ chối thỏa hiệp", "Khải không trao cho Vô Diện thêm quyền lực. Bóng tối lùi lại, nhưng chưa biến mất.", 1, 0),
        new Entry("8A", "Bóng Ma Nạn Nhân Đầu Tiên", "Ta chỉ muốn ngủ yên... nhưng nó không cho ta ngủ...", "Thương xót linh hồn", "Khải cúi đầu trước bức bích họa. Linh hồn tan dần, để lại viên đá nghi lễ.", 2, 0, "Nghi ngờ đây là cái bẫy", "Khải giữ khoảng cách và chuẩn bị vũ khí. Anh sẽ không tin lời thì thầm trong đền cổ.", 0, 1),
        new Entry("8B", "Thầy Hạo", "Con đang biến thành thứ mà con từng sợ hãi nhất đấy.", "Nhờ Hạo giúp kiềm chế", "Hạo trao cho Khải lá bùa ổn định tâm trí và bắt đầu một nghi thức ngắn.", 1, 0, "Dùng sức mạnh để trả thù", "Khải để cơn giận dẫn đường. Bóng tối trong anh mạnh lên cùng những vết cào trên thân cây.", 0, 2),
        new Entry("9A", "Ẩn Sĩ Mù", "Máu tự nguyện khác máu cưỡng ép. Nghi lễ sẽ biết sự khác biệt đó, con trai à.", "Tìm người tự nguyện hiến máu", "Ẩn Sĩ chấp nhận vật hiến tế sạch. Nghi lễ có cơ hội giải thoát những linh hồn bị mắc kẹt.", 2, 0, "Cưỡng ép lấy máu", "Nghi thức vẫn tiếp tục, nhưng những ký tự trên đá chuyển sang màu đen.", 0, 2),
        new Entry("9B", "Lão Rèn Cốc", "Thứ thép này sẽ cắn vào thịt quỷ như cắn vào thịt người. Nhưng ta không đảm bảo cậu sống sót khi rèn nó.", "Rèn Kiếm Khắc Quỷ (cần đủ 3 Quặng Quỷ)", "Lão Cốc nhóm lò. Tiếng búa vang lên khi lưỡi kiếm đặc biệt dần thành hình.", 0, 0, "Săn toàn bộ đạn bạc còn sót", "Khải chọn gom đạn bạc để có lợi thế ở trận chiến sắp tới.", 0, 0, "HasOre3"),
        new Entry("10", "Nhi", "Dù ngày mai có chuyện gì, tôi vẫn ở đây. Anh không phải một mình đâu.", "Ngủ và gặp Vô Diện trong mơ", "Khải khép mắt. Trong giấc mơ, căn phòng trắng đầy những mảnh ký ức đang chờ anh.", 0, 0, "Thức trắng chuẩn bị vũ khí", "Khải kiểm tra lại vũ khí và vật phẩm đến gần sáng. Không còn thời gian để do dự.", 0, 0),
        new Entry("11", "Vô Diện", "Ngươi và ta... vốn dĩ đâu khác nhau là mấy.", "Thấu hiểu nỗi đau của hắn, đề nghị cộng sinh", "Khải nhìn thấy ký ức của kẻ bên trong mình. Lần đầu tiên, cả hai cùng im lặng.", 0, 0, "Thấu hiểu nỗi đau của hắn, đề nghị cộng sinh", "Vô Diện lắc đầu: ‘Ngươi chưa hiểu gì cả.’ Căn phòng trắng bắt đầu tan đi.", 0, 0, "CanBang", "NotBalanced", "Cự tuyệt, khẳng định ý chí", "Khải khẳng định ý chí của mình. Vô Diện lùi vào bóng tối trong căn phòng trắng.", 3, 0),
        new Entry("12", "Ẩn Sĩ Mù", "Giữ vững tâm trí! Đừng để nó len vào từng khoảnh khắc con lơ là!"),
        new Entry("13A", "Nhi", "Để tôi cùng gánh với anh. Anh không cần phải biến mất một mình.", "Tự nguyện hy sinh", "Khải bước vào vòng sáng một mình, chấp nhận cái giá cuối cùng để giải thoát những người còn lại.", 0, 0, "Để Nhi cùng gánh", "Nhi nắm lấy tay Khải. Những người còn lại cùng giữ vòng nghi lễ ổn định.", 0, 0, "NhiTrust", null, "Dùng Trâm Cài làm vật dẫn, cả hai gánh nhẹ hơn", "Trâm Cài phát sáng trong vòng nghi lễ. Nhi cùng Khải giữ lấy phần sức nặng còn lại.", 0, 0, "HasTram"),
        new Entry("13B", "Vô Diện", "Ngươi thắng rồi đấy... nhưng ngươi có chắc phần ‘ngươi’ còn thắng không?", "Cắt đứt hoàn toàn sức mạnh quỷ", "Khải chặt đứt liên kết với Vô Diện, dù biết mình sẽ yếu đi vĩnh viễn.", 0, 0, "Giữ lại một phần sức mạnh", "Khải giữ lại sức mạnh còn sót. Ranh giới giữa hai ý thức vẫn chưa rõ ràng.", 0, 0),
        new Entry("13C", "Vô Diện", "Cảm ơn vì đã cho ta mượn thân xác này lâu đến vậy."),
        new Entry("14", "Dân Làng (tổng hợp)", "Quảng trường dần hồi sinh. Có người cúi đầu cảm ơn Khải; có người lùi lại khi nhìn thấy bóng tối trong anh.", "Rời thị trấn bình yên", "Khải rời quảng trường trong ánh nắng sớm. Người dân để anh đi mà không còn sợ hãi.", 0, 0, "Rời thị trấn cô độc", "Khải rời đi giữa những ánh nhìn dè chừng. Câu chuyện của anh chưa thể được tha thứ hoàn toàn.", 0, 0, "LT_Cao", "QT_Cao", "Đọc lại cuốn nhật ký", "Khải nhận ra nét chữ trong nhật ký trùng với nét chữ của Hạo. Dưới am thờ còn một bí mật.", 0, 0, "SecretEligible"),
        new Entry("15A", "Thầy Hạo - Thật Diện Mạo", "Ta chỉ muốn sống thêm... một lần nữa thôi... con hiểu không?", "Vạch trần Hạo, phá vòng lặp", "Khải đối mặt với Hạo và ký ức của các nạn nhân trước mình. Lời nguyền có thể chấm dứt tại đây.", 0, 0, null, null, 0, 0),
        new Entry("15B", "Thầy Hạo", "Vòng lặp sẽ luôn tìm được người kế tiếp thôi...", "Lặng lẽ rời khỏi hầm mộ", "Khải rời đi trong im lặng. Cánh cửa đóng lại, để vòng lặp tìm người kế tiếp.", 0, 0, null, null, 0, 0)
    };

    [MenuItem("Tools/Dialogue/Create Session Dialogue Assets")]
    private static void CreateAssets()
    {
        EnsureFolder("Assets/Data"); EnsureFolder(Root);
        foreach (Entry entry in Entries)
        {
            string folder = Root + "/Session_" + entry.session;
            EnsureFolder(folder);
            string path = folder + "/Dialogue_Session_" + entry.session + ".asset";
            DemoDialogueAsset asset = AssetDatabase.LoadAssetAtPath<DemoDialogueAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DemoDialogueAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            SerializedObject so = new SerializedObject(asset);
            so.FindProperty("sessionId").stringValue = entry.session;
            so.FindProperty("npcName").stringValue = entry.npc;
            SerializedProperty nodes = so.FindProperty("nodes"); nodes.arraySize = 1;
            SerializedProperty node = nodes.GetArrayElementAtIndex(0);
            node.FindPropertyRelative("text").stringValue = entry.text;
            node.FindPropertyRelative("nextNodeIndex").intValue = -1;
            SerializedProperty choices = node.FindPropertyRelative("choices");
            choices.arraySize = entry.first == null ? 0 : entry.third != null ? 3 : entry.second == null ? 1 : 2;
            if (entry.first != null)
            {
                SetChoice(choices.GetArrayElementAtIndex(0), entry.session, "A", entry.first, entry.firstResult, entry.firstLT, entry.firstQT, entry.firstFlag);
                if (entry.second != null)
                    SetChoice(choices.GetArrayElementAtIndex(1), entry.session, "B", entry.second, entry.secondResult, entry.secondLT, entry.secondQT, entry.secondFlag);
                if (entry.third != null)
                    SetChoice(choices.GetArrayElementAtIndex(2), entry.session, "C", entry.third, entry.thirdResult, entry.thirdLT, entry.thirdQT, entry.thirdFlag);
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Dialogue assets ready", $"Created or updated {Entries.Length} session dialogue assets under {Root}. Assign an asset to DemoNpcDialogueProfile > Dialogue Asset.", "OK");
    }

    private static void SetChoice(SerializedProperty p, string session, string id, string label, string result, int lt, int qt, string requiredFlag)
    {
        p.FindPropertyRelative("sessionId").stringValue = session;
        int option = id == "A" ? 1 : id == "B" ? 2 : 3;
        if (session == "1") id = "1";
        else if (session == "1.5") id = option == 1 ? "1A" : "1B";
        else if (session == "2A") id = option == 1 ? "2A1" : "2A2";
        else if (session == "2B") id = option == 1 ? "2B1" : "2B2";
        else if (session == "3") id = option == 1 ? "3A" : "3B";
        else if (session == "4A" || session == "4B" || session == "6A" || session == "6B"
            || session == "7A" || session == "7B" || session == "8A" || session == "8B"
            || session == "9A" || session == "9B") id = session + option;
        else if (session == "5" || session == "10") id = session + (option == 1 ? "A" : "B");
        else if (session == "11") id = option == 1 ? "11A_Ok" : option == 2 ? "11A_Fail" : "11B";
        else if (session == "13A") id = option == 1 ? "13A_Hy" : option == 2 ? "13A_Nhi" : "13A_Tram";
        else if (session == "13B") id = option == 1 ? "13B_Cat" : "13B_Giu";
        else if (session == "14") id = option == 1 ? "14A_Sach" : option == 2 ? "14A_Loi" : "14B";
        else if (session == "15A") id = "15A";
        else if (session == "15B") id = "15B";
        p.FindPropertyRelative("choiceId").stringValue = id;
        p.FindPropertyRelative("label").stringValue = label;
        p.FindPropertyRelative("requiredFlag").stringValue = requiredFlag ?? string.Empty;
        p.FindPropertyRelative("resultText").stringValue = result;
        p.FindPropertyRelative("nextNodeIndex").intValue = -1;
        p.FindPropertyRelative("ltDelta").intValue = lt;
        p.FindPropertyRelative("qtDelta").intValue = qt;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    private sealed class Entry
    {
        public readonly string session, npc, text, first, firstResult, second, secondResult, third, thirdResult;
        public readonly string firstFlag, secondFlag, thirdFlag;
        public readonly int firstLT, firstQT, secondLT, secondQT, thirdLT, thirdQT;
        public Entry(string session, string npc, string text, string first, string firstResult, int firstLT, int firstQT,
            string second, string secondResult, int secondLT, int secondQT, string firstFlag = null, string secondFlag = null,
            string third = null, string thirdResult = null, int thirdLT = 0, int thirdQT = 0, string thirdFlag = null)
        { this.session=session; this.npc=npc; this.text=text; this.first=first; this.firstResult=firstResult;
          this.firstLT=firstLT; this.firstQT=firstQT; this.second=second; this.secondResult=secondResult;
          this.secondLT=secondLT; this.secondQT=secondQT; this.firstFlag=firstFlag; this.secondFlag=secondFlag;
          this.third=third; this.thirdResult=thirdResult; this.thirdLT=thirdLT; this.thirdQT=thirdQT; this.thirdFlag=thirdFlag; }
        public Entry(string session, string npc, string text) : this(session,npc,text,null,null,0,0,null,null,0,0) { }
    }
}

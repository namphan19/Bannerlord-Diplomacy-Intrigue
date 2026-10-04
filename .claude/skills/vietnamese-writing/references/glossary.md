# Bảng thuật ngữ — Diplomacy & Intrigue

Mỗi khái niệm một cách gọi. Cột "Trên màn hình" là nhãn tiếng Anh người chơi thấy trong game:
giữ nguyên nhãn đó khi nhắc tới nút, tab hay mức, và dùng cột "Tiếng Việt" khi giải thích khái
niệm.

Thêm dòng mới khi gặp khái niệm chưa có. Đổi một dòng đã có thì phải sửa mọi văn bản đang dùng
nó, nên chỉ đổi khi có lý do rõ ràng.

**Hai loại văn bản, hai cách dùng bảng này.** *Văn xuôi* (sổ tay, trang phát hành, ghi chú) thì
giữ nguyên nhãn tiếng Anh khi nhắc tới, vì người đọc đang nhìn nhãn đó trên màn hình. *File chữ của
game* (`ModuleData/Languages/VI/di_strings.xml`) thì **dịch hết**, kể cả tên tab và tên mức: người
chơi bản vá nhìn thấy tiếng Việt ở mọi nhãn vanilla, để tiếng Anh ở nhãn của ta thì đọc lệch.
Bảng này dùng cho cả hai; chỗ nào ghi "giữ nguyên tiếng Anh" là cho văn xuôi.

## Thuật ngữ bản vá cộng đồng đã xác nhận (story 4.2 ST-2, 2026-10-04)

Đọc từ 23.808 chuỗi của bản vá, nối với tiếng Anh qua id băm. Những từ dưới đây bản vá dịch
sẵn, nên ta theo bản vá (4.2 R3):

| Tiếng Anh | Bản vá dùng | Ta dùng |
|---|---|---|
| kingdom | Vương quốc | vương quốc |
| clan | Gia tộc | gia tộc |
| influence | Ảnh hưởng | **ảnh hưởng** trong file chữ; văn xuôi giữ "influence" |
| army | Quân đội | quân đội |
| party (on the map) | Đội quân | đội quân |
| siege | Vây hãm | vây hãm |
| diplomacy | Ngoại giao | ngoại giao |
| policies | Chính sách | chính sách |
| clans (tab) | Gia tộc | Gia tộc |
| armies (tab) | Quân đội | Quân đội |
| denar | Denars | denar |
| tribute | Cống nạp | cống nạp |
| vassal | Chư hầu | chư hầu |
| loyalty | Lòng trung thành | lòng trung thành |
| renown | Danh tiếng | danh tiếng |
| settlement | Khu định cư | khu định cư |
| governor | Thống đốc | thống đốc |
| warden | Hộ Vệ | hộ vệ |
| court (in prose) | triều đình | triều đình |

**`Hold` là bẫy.** Bản vá dịch `Hold` thành **"Giữ"**, vì trong game `hold` là động từ "giữ".
`Hold` của ta là khái niệm riêng (độ ràng buộc giữa chư hầu và bá chủ), và **không được theo bản
vá ở chỗ này**. Giữ "Hold" như bảng dưới.

## Chỗ xung đột, chờ anh quyết (4.2 D3)

Bản vá và bảng thuật ngữ cũ không khớp. Mặc định theo bản vá, nhưng năm chỗ này là khái niệm
chính trị nên anh chọn:

| Thuật ngữ | Bản vá | Bảng cũ của ta | Ghi chú |
|---|---|---|---|
| town | Thị trấn | thành phố | "thị trấn" đúng nghĩa game; "thành phố" rộng hơn |
| fief | lãnh thổ / lãnh địa (tab) | thái ấp | ba cách, và bản vá tự dùng ba từ khác nhau |
| ruler | Người cai trị | vua | "vua" là danh xưng, "người cai trị" là chức vụ |
| realm (tab) | không có | — | của riêng ta |
| court (tab) | không có | — | của riêng ta |

Bản vá **không có** cho: hegemon, grievance, casus belli, handler, counter-intelligence,
treasurer, manor, settler, hold (khái niệm của ta), realm, court (tab). Mười từ này hoàn toàn
do ta chọn.

## Chiến tranh và hòa bình

| Tiếng Anh | Tiếng Việt | Trên màn hình | Ghi chú |
|---|---|---|---|
| war exhaustion | độ kiệt quệ | | "độ kiệt quệ chiến tranh" ở lần đầu |
| war score | điểm chiến tranh | | |
| casus belli | cớ tuyên chiến | | lần đầu có thể ghi *(casus belli)* |
| legitimacy (của cớ tuyên chiến, 0–1) | tính chính đáng | | **không** dùng "chính danh" cho nghĩa này |
| conquest | chinh phạt | | |
| white peace | hòa bình trắng | White peace only | |
| peace table | bàn hòa bình | | |
| sue for peace / negotiate peace | xin hòa / đàm phán hòa bình | Sue for peace / Negotiate peace | |
| call to arms | lời gọi tham chiến | | |
| weariness | độ mỏi mệt | | độ kiệt quệ còn lại sau chiến tranh |
| claim | yêu sách | | "yêu sách lãnh thổ"; không để "claim" trần trong câu |
| fabricate a claim | ngụy tạo yêu sách | | |
| fief | thái ấp | | **xung đột, chờ anh** |
| town / castle / village | thành phố / lâu đài / làng | | **town xung đột, chờ anh**; castle và làng khớp bản vá |
| siege | cuộc vây thành | | bản vá dùng "vây hãm" |
| raid | cướp phá | | |
| indemnity | tiền bồi thường | | |

## Hiệp ước và lòng tin

| Tiếng Anh | Tiếng Việt | Trên màn hình | Ghi chú |
|---|---|---|---|
| treaty / pact | hiệp ước | | |
| truce | hưu chiến | Truce | |
| non-aggression pact | hiệp ước bất tương xâm | Non-aggression pact | |
| defensive pact | hiệp ước phòng thủ | Defensive pact | |
| alliance | liên minh | Alliance | |
| tributary pact | hiệp ước triều cống | Tributary pact | |
| tribute | cống nạp, tiền cống | | |
| vassalage | quan hệ chư hầu | Vassalage | |
| renounce / break a treaty | xé bỏ / phá hiệp ước | | |
| trust | lòng tin | | |
| relation | quan hệ | | chỉ số quan hệ vanilla giữa hai nhân vật |

## Bá quyền

| Tiếng Anh | Tiếng Việt | Trên màn hình | Ghi chú |
|---|---|---|---|
| hegemon | bá chủ | | bản vá không có |
| patron | bá chủ (của một chư hầu cụ thể) | | tránh "người bảo trợ" |
| vassal | chư hầu | | |
| sphere | vùng ảnh hưởng | | |
| Hold | Hold | Hold | giữ nguyên; **không** theo bản vá ("Giữ" là động từ). Giải thích một lần là "độ ràng buộc giữa chư hầu và bá chủ" |
| submit / kneel | quy phục | Kneel to them | |
| revolt / secede | nổi dậy / ly khai | Declare independence | |
| defiance mark | dấu bất tuân | | |
| poach | lôi kéo | Court them | |
| greed | lòng tham | | |
| ambition | tham vọng | | |
| dominance | sức áp đảo | | |

## Triều đình

| Tiếng Anh | Tiếng Việt | Trên màn hình | Ghi chú |
|---|---|---|---|
| court | triều đình | Court | khớp bản vá |
| clan / house | gia tộc | | "nhà" được dùng khi câu cần ngắn |
| ruler / crown | vua / ngai vàng | | **ruler xung đột, chờ anh**; bản vá dùng "Người cai trị" |
| ruling clan | hoàng tộc | | |
| crown legitimacy (0–100) | tính chính danh | | của ngai vàng; khác "tính chính đáng" |
| grievance | mối oán | | "mối oán" ngắn và rõ hơn "sự oán hận"; bản vá không có |
| loyalty | lòng trung thành | | |
| bloc / faction | phe | | |
| agenda | chủ trương | | |
| Doves / Hawks / Autonomists / Centralists / Pretenders | phe hòa / phe chiến / phe tự trị / phe tập quyền / phe tranh ngôi | Doves, Hawks, ... | tên trên màn hình giữ tiếng Anh |
| pretender | người tranh ngôi | | người đang giữ yêu sách ngai vàng |
| claimant | người đòi ngôi | | trong một cuộc kế vị cụ thể |
| succession | kế vị | | |
| contested succession | kế vị tranh chấp | | |
| internal war / civil war | nội chiến | | |
| the rising | phe nổi dậy | | |
| concede | nhận thua | Concede | |
| change sides | đổi phe | | |
| cadet branch | chi thứ | | |

## Giữ nguyên tiếng Anh — **chỉ trong văn xuôi**

influence, tên các tab (Diplomacy, Realm, Court, Clans, Fiefs, Policies, Armies),
tên các mức (FRESH, STRAINED, WEARY, EXHAUSTED, BREAKING, LOYAL, SERVING, RESISTING, DEFIANT,
RELIABLE, TRANSACTIONAL, DISAFFECTED, DEFECTION RISK, Failing, Questioned, Secure), tên nút,
Kingdom screen, Encyclopedia, Mod Configuration Menu, tên vương quốc và nhân vật.

**Không áp danh sách này cho `Languages/VI/di_strings.xml`.** Trong file chữ của game, tên tab và
tên mức cũng dịch: người chơi bản vá thấy tiếng Việt ở nhãn vanilla, nhãn tiếng Anh của ta sẽ
lạc trong đó. Danh sách trên là để **nhắc tới** nhãn tiếng Anh trong văn xuôi.

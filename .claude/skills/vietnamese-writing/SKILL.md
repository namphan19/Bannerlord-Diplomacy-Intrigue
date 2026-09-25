---
name: vietnamese-writing
description: Write, translate or rewrite Vietnamese text that people will actually read, so that it sounds like it was written by a careful Vietnamese writer rather than translated word for word from English. Use it for every Vietnamese deliverable in this project - the Vietnamese player handbook and other artifacts, player-facing strings, release notes, Nexus pages, reports and summaries for the lead - and whenever the user asks to translate something into Vietnamese, says existing Vietnamese reads badly, stiffly or "like Google Translate", or asks for a Vietnamese version of a doc. Also use it when reviewing someone else's Vietnamese. Chat replies to the lead do not need the full procedure, but the style rules still apply.
---

# Viết tiếng Việt cho hay

Mục tiêu: người Việt đọc thấy tự nhiên, rõ ràng, không vấp, không có mùi dịch máy. Nghĩa phải
đúng từng chi tiết của bản gốc, nhưng câu chữ phải là của tiếng Việt.

Lỗi hay gặp nhất không phải sai từ, mà là **giữ nguyên cấu trúc câu tiếng Anh rồi thay từng
chữ**. Kết quả đúng nghĩa nhưng đọc lên thấy lạ. Phần lớn skill này là cách nhận ra và gỡ những
cấu trúc đó.

## Quy trình

1. **Hiểu hết bản gốc trước khi viết.** Với mỗi đoạn, tự trả lời: đoạn này muốn người đọc biết
   gì, làm gì? Nếu bản gốc là tài liệu của dự án, đối chiếu các con số với code hoặc hằng số khi
   làm được. Bản dịch lặp lại một con số sai vẫn là bản dịch sai.
2. **Chốt thuật ngữ.** Dùng [references/glossary.md](references/glossary.md) cho dự án này. Mỗi
   khái niệm một cách gọi, dùng từ đầu đến cuối. Gặp khái niệm chưa có trong bảng thì chọn một
   cách gọi, dùng nhất quán, và thêm vào bảng.
3. **Viết lại theo ý, không theo chữ.** Đọc một câu gốc, gập lại, rồi viết câu tiếng Việt diễn
   đạt ý đó. Cho phép tách một câu dài thành hai, gộp hai câu ngắn thành một, đổi thứ tự vế, đổi
   chủ ngữ.
4. **Soát theo danh sách lỗi** ở mục dưới. Mỗi câu nghi ngờ, đọc thầm thành tiếng: nếu một người
   Việt không bao giờ nói như thế, sửa.
5. **Soát lần cuối về nghĩa**: so từng đoạn với bản gốc. Không thêm ý, không bớt ý, không làm mềm
   hay làm gắt hơn bản gốc. Số liệu, điều kiện, ngưỡng phải khớp tuyệt đối.

## Những lỗi cần gỡ

Ví dụ trước và sau, lấy từ bản dịch thật của dự án, nằm ở
[references/examples.md](references/examples.md). Đọc file đó trước khi viết lại một văn bản dài.

**Bị động kiểu Anh.** Tiếng Anh dùng bị động rất nhiều; tiếng Việt thì ít hơn nhiều, và "bị"
mang sắc thái tiêu cực. Chuyển sang chủ động, hoặc bỏ chủ ngữ.
"Hiệp ước được ký bởi hai vương quốc" → "Hai vương quốc ký hiệp ước".

**Lạm dụng "bạn".** Tiếng Việt thường lược chủ ngữ khi đã rõ ai làm. Mỗi câu một chữ "bạn" là quá
nhiều. Giữ "bạn" ở chỗ cần nhấn hoặc cần phân biệt với AI và người khác.

**Danh từ hóa nặng nề.** "Việc thực hiện sự thay đổi" → "thay đổi". Bớt "sự", "việc", "tính" khi
một động từ nói được.

**Dịch sát thành ngữ và lối nói hình ảnh.** "Is not a mood" không thành "không phải là tâm
trạng". Tìm cách người Việt nói cùng ý, hoặc nói thẳng ý đó ra.

**Từ nối và hư từ thừa.** "thì", "là", "mà", "đó", "của", "một" chèn vào mỗi câu làm văn lê thê.
"Nếu bạn làm điều đó thì bạn sẽ..." → "Làm vậy, bạn sẽ...".

**Câu dài nhiều mệnh đề phụ.** Tiếng Anh xếp mệnh đề "which/that/when" nối đuôi. Tiếng Việt đọc
dễ hơn khi tách câu, hoặc đưa điều kiện lên đầu: "Khi..., ...".

**Trật tự thông tin.** Tiếng Việt quen đi từ bối cảnh đến kết luận, từ điều kiện đến kết quả. Nếu
câu gốc đặt kết quả trước, cân nhắc đảo lại.

**Hán Việt: đúng chỗ, không phô.** Thuật ngữ chính trị, lịch sử (chư hầu, bá chủ, triều cống,
chính danh) nên dùng Hán Việt vì chính xác và quen. Lời giải thích thường ngày thì dùng từ thuần
Việt cho dễ đọc. Tránh từ Hán Việt hiếm chỉ để câu nghe "sang".

**Trộn Anh–Việt tùy tiện.** Chỉ giữ tiếng Anh cho những gì người chơi *nhìn thấy trên màn hình
game*: tên nút, tên tab, tên mức (band), tên tài nguyên. Mọi khái niệm khác dịch theo bảng thuật
ngữ. Lần đầu nhắc một thuật ngữ quan trọng, có thể ghi tiếng Anh trong ngoặc một lần, sau đó
thôi.

**Giọng văn.** Sổ tay game là lời một người chơi lâu năm giải thích cho người mới: gọn, tự tin,
có chút sắc sảo, không màu mè, không khẩu hiệu, không dùng từ lóng mạng. Giữ nguyên độ dí dỏm
của bản gốc nếu có, nhưng bằng cách nói của tiếng Việt.

## Quy ước trình bày

- **Số**: dấu phẩy thập phân, dấu chấm hàng nghìn: `0,95`, `20.000`, `1,3 lần`. Giữ nguyên dạng
  số khi trích đúng một nhãn trên màn hình game (game hiển thị tiếng Anh).
- **Khoảng**: `40–69` (gạch ngang dài, không cách). Dấu trừ dùng `−`, không dùng gạch nối.
- **Ngoặc kép**: “…” cho lời trích; tên nút và nhãn in đậm hoặc để nguyên, không cần ngoặc.
- **Dấu câu**: không có dấu cách trước `:`, `;`, `?`, `!`. Dấu hai chấm không nối chuỗi nhiều lần
  trong một câu.
- **Viết hoa**: chỉ viết hoa chữ đầu câu và tên riêng. Tiêu đề không viết hoa mọi chữ kiểu Anh.
- **Mã hóa**: Unicode dựng sẵn (NFC). Không dùng chữ tổ hợp dấu rời.

## Khi viết cho dự án Diplomacy & Intrigue

- Bản tiếng Anh là bản chính; bản tiếng Việt phải nói đúng những gì bản tiếng Anh nói, trừ khi
  bản tiếng Anh sai so với code. Khi đó sửa bản tiếng Việt cho đúng code, và **báo lại** cho
  người yêu cầu những chỗ bản tiếng Anh cũng cần sửa. Không âm thầm sửa lệch hai bản.
- Tên nút, tab, mức và thông báo trong game giữ nguyên tiếng Anh như trên màn hình
  (Diplomacy, Realm, Court, Negotiate peace, FRESH, RELIABLE...), kèm lời giải thích tiếng Việt.
- Người đọc là game thủ Việt đã quen Bannerlord: không cần giải thích influence, renown, denar
  là gì.

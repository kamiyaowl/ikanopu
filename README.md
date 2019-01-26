# ikanopu

![ikanopu](https://user-images.githubusercontent.com/4300987/51441131-addb7100-1d11-11e9-8dad-fde8de0c3b6f.jpg)

ikanopuはSplatoon2のプライベートマッチの画面から、Discordのボイスチャットの部屋割りを自動化するbotです

# Environments

* Windows 10
* Visual Studio 2017
* Capture Board(AVT-C878)

# Install

*詳細はDiscord公式にあるため割愛します*

1. Discord 開発者サイトからbotを作成
1. 作成時に取得できるTokenをメモしておきます(ikanopu 初回起動時に入力)
1. botのOAuth認証URLを作成し、使用したいギルドに追加します
1. ギルド内メンバーのModifyができる権限(役職)をbotに付与します

# Usage

## 起動・終了

* `ikanopu.exe`を起動します
  * (初回起動後、先程のDiscord botのTokenを入力します。)
* キャプチャデバイスの画面が表示され、botが使用できるようになります。
* キャプチャ画面がアクティブな状態でESCキーを押すと終了します
    * 同時に設定を`config.json`と`secret.json`に保存します

## 設定項目

以下の主要な設定について確認してください。`ikanopu.exe`と同じディレクトリに作成された`config.json`を参照します。

[実装 GlobalConfig.cs](https://github.com/kamiyaowl/ikanopu/blob/master/ikanopu/Config/GlobalConfig.cs)

### CameraIndex
使用するキャプチャデバイスのデバイス番号を設定します。カメラデバイスが1つだけであれば0、それ以外であれば1,2,等を指定します

### AlphaVoiceChannelId / BravoVoiceChannelId / LobbyVoiceChannelId 
遷移させるボイスチャットのIDを登録します。ボイスチャットIDの確認は、Discordクライアントを開発者モードを設定の上、当該のチャンネルを右クリックして取得するか、`!pu debug vc users`で取得可能です。

* Alpha - アルファチーム
* Bravo - ブラボーチーム
* Lobby - `!pu lobby`を発行した際に全員をこのチャンネルに移動します

### RegistereUsers
特徴量画像を登録済みのユーザです。後述の`!pu register [user] [index]`コマンドで登録することが可能です。(手動編集でも可能です。)

## Discord上での使い方

Discordの任意テキストチャンネル上で以下のコマンド等が使用可能です。詳細は`!pu`コマンドで表示されるヘルプを参照してください。
注意点として**ボイスチャット移動系のコマンドは、ステータスがオフライン以外のユーザに対してのみ有効です**。オンライン状態を隠したままだと適用されないため注意してください。

[実装 PuModule.cs](https://github.com/kamiyaowl/ikanopu/blob/master/ikanopu/Module/PuModule.cs)

主要なコマンドについて以下に紹介します。

### `!pu`
ヘルプを表示します。(最後に)

### `!pu lobby`
現在`Alpha/Bravo`チャンネルにいるユーザを`Lobby`チャンネルに移動させます。

### `!pu detect [move=true] [cropIndex=-1] [uploadImage=true] [preFilter=true] [moveWatcher=true]`
現在のキャプチャ画像から画像認識を行い、ユーザを`Alpha/Bravo`チャンネルに移動させます。**ikanopuを起動させているユーザが、ブキ選択画面から抜けていてユーザ一覧が見えている状態にあることが必須です。**

`[move=true] [cropIndex=-1] [uploadImage=true] [preFilter=true]`についてはオプションなので基本は省略し、`!pu detect`だけで実行可能です。

初めて起動しており誰も登録されていない場合は、認識エラーで結果が帰ってこないため以下2コマンドのいずれかを叩いた後に、後述の`!pu register [user] [index]`を使用して登録を行ってください。

#### 観戦者がいない場合
`!pu detect false 0 true false`

#### 観戦者がいる場合
`!pu detect false 1 true false`

分けているのは観戦者の有無で切り出す領域を切り替えているためです。(通常は認識精度の高い方を採用して返しています。)

### `!pu register [user] [index]`
直近に画像認識を行った中から、名前の画像と未登録のユーザのDiscord IDを紐つけて登録します。
indexに指定スべき数字を知るためには、`!pu register show images`を実行します。

### `!pu register show now true`
現在登録済のユーザ一覧を画像つきで返します。

# Known Issue

## Discordのレスポンスが遅れて帰ってくる

Gateway Blockingによって一括で画像を返すコマンドなどにリトライがかかり、分割して結果が帰る場合があります。

## 画像認識精度

~~画像処理プログラミングに対する理解がしょぼいため~~画像認識を誤る場合があります。
具体的には以下の処理で実装していますが、短い名前が複数あった場合などに認識を誤る事象を確認しています。
* 元画像から名前の領域を切り取り
* グレースケール→2値化処理で名前以外の背景を削除
* 名前の画像から、画像の特徴点(KeyPoint)を抽出
* 登録されている画像の特徴点と、キャプチャした名前画像の特徴点比較
* マッチングした特徴点の距離総和と特徴点の数からもっとも親しいものを抽出

*Splatoon2の名前画像は微妙に傾いており、単純なテンプレートマッチやXORではうまくいかなそうでした。回転も考慮してマッチングを行うか、傾きを補正してしまうなどより良い手法があればぜひ...。*

# License

[MIT License](https://github.com/kamiyaowl/ikanopu/blob/master/LICENSE)
___

# 2019/01/20時点のコマンドリスト(参考まで)
```
pu
コマンド一覧を表示

pu lobby
ボイスチャット参加者をロビーに集めます。
アルファ、ブラボー、ロビーのVCに参加していて、ステータスがオフラインではないユーザが対象です

pu detect [move] [cropIndex] [uploadImage] [preFilter]
画像認識を行いボイスチャットを遷移させます。
ステータスをオフラインにしていないユーザすべてが対象です。
[move]: (optional: true) 推測結果からユーザを移動させる場合はtrue
[cropIndex]: (optional: -1) 切り出す領域を設定します。-1の場合は結果の良い方を採用
[uploadImage]: (optional: true) 認識に使用した画像を表示する場合はtrue
[preFilter]: (optional: true) 認識できなかった結果を破棄する場合はtrue

pu rule [nawabari]
ステージとルールに悩んだらこれ
[nawabari]: (option: false) ナワバリバトルを含める場合はtrue

pu buki [count]
ブキに悩んだらこれ
[count]: (option: 8) おみくじの回数。8人分用意すればいいよね

pu register [user] [index]
画像とDiscord Userの関連付けを追加します
[user]: 追加するユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)
[index]: 削除するインデックス。必ず!pu register showで確認してください。

pu register remove [index] [delete]
画像とDiscord Userの関連付けを削除します
[index]: 削除するインデックス。必ず!pu register showで確認してください。
[delete]: (option: false) 確認用。本当に削除する場合はtrue

pu register show now [showImage] [useBitmap]
登録済一覧を表示します
[showImage]: (optional: false) 登録画像も一緒に表示する場合はtrue
[useBitmap]: (optional: false) bitmapのオリジナル画像が欲しい場合はtrue

pu register show images [useBitmap]
現在登録可能な画像一覧を返します。(!pu detect実行時にキャッシュされます
[useBitmap]: (optional: false) bitmapのオリジナル画像が欲しい場合はtrue

pu config get [name]
config.jsonの内容を表示します
[name]: 子要素名、--all指定するとすべて表示

pu config set [name] [value]
config.jsonの値を書き換えます。AdminUsersに追加されていることが条件です
[name]: 子要素名、--all指定するとすべて表示
[value]: 設定したい値

pu config sync
RegisterUsersにあるユーザー名をDiscordと同期します

pu debug echo [text]
俺がオウムだ
[text]: 適当なテキスト

pu debug capture
現在のキャプチャデバイスの画像を取得します

pu debug userinfo [user]
ユーザー情報を返します
[user]: (optional: bot_id) ユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)。省略した場合は自身の情報

pu debug move [user] [vc]
ボイスチャンネル移動テスト
[user]: ユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)
[vc]: ボイスチャンネルID

pu debug vc users
ボイスチャットに参加しているユーザー一覧を返します

pu debug clean images [delete]
登録されていない画像キャッシュを削除します
[delete]: (option: false) 確認用。本当に削除する場合はtrue

pu debug clean posts [delete] [limit]
ikanopuのつぶやきをなかったことにする
[delete]: (option: false) 確認用。本当に削除する場合はtrue
[limit]: (optional: 100) 遡って削除する上限数
```
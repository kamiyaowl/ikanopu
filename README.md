# ikanopu

![ikanopu](https://user-images.githubusercontent.com/4300987/51915233-ef5cd200-241d-11e9-929b-de9bcad41a31.gif)

ikanopuはSplatoon2のプライベートマッチの画面から、Discordのボイスチャットの部屋割りを自動化するbotです。

**Alpha/Bravoの通話をロビーでMixするikanokaiwaはこちら https://github.com/taniho0707/ikanokaiwa**

# Environments

* Windows 10
* Visual Studio 2017
* .NET Core 2.1
* [Discord.Net](https://github.com/discord-net/Discord.Net)
* [OpenCVSharp](https://github.com/shimat/opencvsharp)
* Capture Board(AVT-C878)

# Install

## アプリケーションのビルド

Visual Studio 2017以降、もしくは[.NET Core](https://dotnet.microsoft.com/download)を準備します。

### VSの場合

ikanopu.slnを開いたら後はよしなに

### dotnet cliでビルドする場合

#### クローンしてプロジェクトディレクトリに移動
```
$ git clone https://github.com/kamiyaowl/ikanopu.git && cd ikanopu/ikanopu
```

#### Releaseビルドする
```
$ dotnet publish -c Release
```

#### 実行
```
$ cd bin/Release/netcoreapp2.1/publish/
$ dotnet ikanopu.dll
```

## botのアカウント設定

*詳細はDiscord公式にあるため割愛します*

1. Discord 開発者サイトからbotを作成
1. 作成時に取得できるTokenをメモしておきます(ikanopu 初回起動時に入力)
1. botのOAuth認証URLを作成し、使用したいギルドに追加します
1. botにギルド内メンバーのModifyができる権限(役職)を付与します(VoiceChannelの変更に使用します)

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

### RegisterUsers
特徴量画像を登録済みのユーザです。後述の`!pu register [user] [index]`コマンドで登録することが可能です。(手動編集でも可能です。)

## Discord上での使い方

Discordの任意テキストチャンネル上で以下のコマンド等が使用可能です。詳細は`!pu`コマンドで表示されるヘルプを参照してください。
注意点として**ボイスチャット移動系のコマンドは、ステータスがオフライン以外のユーザに対してのみ有効です**。**オンライン状態を隠したままだと適用されないため注意してください。**

[実装 PuModule.cs](https://github.com/kamiyaowl/ikanopu/blob/master/ikanopu/Module/PuModule.cs)

主要なコマンドについて以下に紹介します。

### `!pu`
ヘルプを表示します。(最後に)

### `!pu lobby`
現在`Alpha/Bravo`チャンネルにいるユーザを`Lobby`チャンネルに移動させます。

### `!pu detect [move=true] [cropIndex=-1] [uploadImage=true] [preFilter=true] [moveWatcher=true]`
現在のキャプチャ画像から画像認識を行い、ユーザを`Alpha/Bravo`チャンネルに移動させます。**ikanopuを起動させているユーザが、ブキ選択画面から抜けていてユーザ一覧が見えている状態にあることが必須です。**

`[move=true] [cropIndex=-1] [uploadImage=true] [preFilter=true] [moveWatcher=true]`についてはオプションなので基本は省略し、`!pu detect`だけで実行可能です。

初めて起動しており誰も登録されていない場合は、認識エラーで結果が帰ってこないため以下2コマンドのいずれかを叩いた後に、後述の`!pu register [user] [index]`を使用して登録を行ってください。

#### 観戦者がいない場合
`!pu detect false 0 true false`

#### 観戦者がいる場合
`!pu detect false 1 true false`

分けているのは観戦者の有無で切り出す領域を切り替えているためです。(通常は認識精度の高い方を採用して返しています。)

### `!pu register [user] [index]`
直近に画像認識を行った中から、名前の画像と未登録のユーザのDiscord IDを紐つけて登録します。
indexに指定するべき数字は、認識した画像に直接描画されている数字を指定します。または`!pu register show images`でも登録する画像を見ることができます。

### `!pu register show now true`
現在登録済のユーザ一覧を画像つきで返します。

# Known Issue

## Ubuntu環境およびDocker環境でOpenCVSharpExternがP/Invokeエラーを発生させる

https://github.com/kamiyaowl/ikanopu/issues/24

## 画像認識精度

**うまく認識できなかった画像も、再登録を行うことでかなり精度を改善できることを確認しています。**
認識ミスは異なる特徴量を持った画像と認識しているので、discord idと紐付けることで精度改善が見込めます。

具体的には以下の処理で実装していますが、短い名前が複数あった場合などに認識を誤る事象を確認しています。

* 元画像から名前の領域を切り取り
* グレースケール→2値化処理で名前以外の背景を削除
* 名前の画像から、画像の特徴点(KeyPoint)を抽出
* 登録されている画像の特徴点と、キャプチャした名前画像の特徴点比較
* マッチングした特徴点の距離総和と特徴点の数からもっとも類似する画像を抽出

*Splatoon2の名前画像は微妙に傾いており、単純なテンプレートマッチやXORではうまくいかなそうでした。回転も考慮してマッチングを行うか、傾きを補正してしまうなどより良い手法があればぜひ...。*

# License

[MIT License](https://github.com/kamiyaowl/ikanopu/blob/master/LICENSE)
___

# Command List
```
pu 
コマンド一覧を表示

pu lobby 
ボイスチャット参加者をロビーに集めます。
アルファ、ブラボー、ロビーのVCに参加していて、ステータスがオフラインではないユーザが対象です

pu detect [move] [cropIndex] [uploadImage] [preFilter] [watcherMove]
画像認識を行いボイスチャットを遷移させます。
ステータスをオフラインにしていないユーザすべてが対象です。
move: (option: true) 推測結果からユーザを移動させる場合はtrue
cropIndex: (option: -1) 切り出す領域を設定します。-1の場合は結果の良い方を採用
uploadImage: (option: true) 認識に使用した画像を表示する場合はtrue
preFilter: (option: true) 認識できなかった結果を破棄する場合はtrue
watcherMove: (option: false) 観戦者をAlpha/Bravoチャンネルに移動させる場合はtrue

pu rule [nawabari]
ステージとルールに悩んだらこれ
nawabari: (option: false) ナワバリバトルを含める場合はtrue

pu buki [count]
ブキに悩んだらこれ
count: (option: 8) おみくじの回数。8人分用意すればいいよね

pu register [user] [index]
画像とDiscord Userの関連付けを追加します
user: 追加するユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)
index: 削除するインデックス。必ず!pu register showで確認してください。

pu register remove [index] [delete]
画像とDiscord Userの関連付けを削除します
index: 削除するインデックス。必ず!pu register showで確認してください。負数の場合、最後の登録からのインデックスで指定できます
delete: (option: false) 確認用。本当に削除する場合はtrue

pu register show now [showImage] [useBitmap]
登録済一覧を表示します
showImage: (option: false) 登録画像も一緒に表示する場合はtrue
useBitmap: (option: false) bitmapのオリジナル画像が欲しい場合はtrue

pu register show images [useBitmap]
現在登録可能な画像一覧を返します。(!pu detect実行時にキャッシュされます
useBitmap: (option: false) bitmapのオリジナル画像が欲しい場合はtrue

pu config raw [name]
config.jsonの内容を表示します
name: 子要素名、--all指定するとすべて表示

pu config sync 
RegisterUsersにあるユーザー名をDiscordと同期します
pu config get vc [id]
VoiceChannel情報を返します
id: VoiceChannel ID

pu config get alpha 
AlphaVoiceChannelIdを返します

pu config get bravo 
BravoVoiceChannelIdを返します

pu config get lobby 
LobbyVoiceChannelIdを返します

pu config set vc [target] [id]
VoiceChannel情報を返します
target: 更新先。{alpha, bravo, lobby}のいずれか
id: VoiceChannel ID

pu config set alpha 
AlphaVoiceChannelIdを設定します

pu config set bravo 
BravoVoiceChannelIdを設定します

pu config set lobby 
LobbyVoiceChannelIdを設定します

pu debug echo [text]
俺がオウムだ
text: 適当なテキスト

pu debug capture 
現在のキャプチャデバイスの画像を取得します

pu debug userinfo [user]
ユーザー情報を返します
user: (option: bot_id) ユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)。省略した場合は自身の情報

pu debug move [user] [vc]
ボイスチャンネル移動テスト
user: ユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)
vc: ボイスチャンネルID

pu debug vc users 
ボイスチャットに参加しているユーザー一覧を返します

pu debug clean images [delete]
登録されていない画像キャッシュを削除します
delete: (option: false) 確認用。本当に削除する場合はtrue

pu debug clean posts [delete] [limit]
ikanopuのつぶやきをなかったことにする
delete: (option: false) 確認用。本当に削除する場合はtrue
limit: (option: 100) 遡って削除する上限数
```

# Other

いっしょにイカやりましょう。

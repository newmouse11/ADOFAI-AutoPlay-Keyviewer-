# Auto Fingering

[English](#english)

A Dance of Fire and Ice용 UnityModManager 모드입니다. 게임의 자동 재생(Otto)이 돌 때 레벨 전체의 운지를 사람이 칠 수 있는 형태로 계산하고, 자동 재생이 타일을 치는 순간 배정된 키를 키 뷰어에 켜 줍니다. 자체 뷰어는 없으므로 키 뷰어 모드가 따로 있어야 합니다.

표시만 하는 모드입니다. 입력을 보내지 않고 `RDC.auto`도 바꾸지 않으므로 판정과 결과에는 영향이 없습니다.

## 설치

1. [UnityModManager](https://github.com/newman55/unity-mod-manager)가 설치된 게임과 [지원하는 키 뷰어](#지원하는-뷰어) 중 하나가 필요합니다.
2. [릴리스 페이지](https://github.com/newmouse11/ADOFAI-AutoPlay-Keyviewer-/releases/latest)에서 `AutoFingering-<버전>.zip`을 받아 UnityModManager로 설치하거나, 압축을 풀어 `게임 폴더/Mods/AutoFingering/`에 `Info.json`과 `AutoFingering.dll`이 놓이게 합니다.
3. 게임을 켭니다. 설정 창은 기본 단축키 Ctrl+F10으로 엽니다.

## 사용법

1. 먼저 설정 창의 `Auto Fingering` 항목에서 롤 방식을 고릅니다. 운지는 자동 재생이 시작될 때 이 설정으로 계산합니다.
2. 레벨 에디터에서 레벨을 엽니다. 정식 판 게임에서 자동 재생은 사실상 에디터의 Otto 버튼(단축키 A, 타일을 선택한 상태에서는 Alt+A)으로만 켤 수 있습니다.
3. Otto를 켜고 플레이합니다. 계산이 끝나는 대로 키가 켜지기 시작합니다. 같은 레벨과 설정으로 다시 시작하면 다시 계산하지 않습니다.

## 설정

| 항목 | 값 | 기본값 |
|---|---|---|
| 언어 | 게임 설정 / 한국어 / English | 게임 설정 |
| 롤 방식 | 안밖 / 밖안 / 계단 / 역계단 | 안밖 |
| 주손 | 오른손 / 왼손 | 오른손 |
| 기반 | 2키 / 4키 | 2키 |
| 최대 사용 키 | 4, 6, 8, 10, 12, 16, 전체 (뷰어의 키 수를 넘지 못함) | 전체 |
| 2키 기반 최대 속도 | 테스트, 슬라이더 또는 `-`/`+` 버튼, 480-2000 BPM | 923 BPM |
| 키 역할 편집 | 슬롯마다 손과 손가락, 또는 사용 안 함. 키 개수별로 저장 | 뷰어 배열 순서로 추정 |

2키 기반 최대 속도는 두 키를 번갈아 칠 때 낼 수 있는 분당 타수(BPM)입니다. Jipper 오버레이의 CBPM과 같은 기준이고, 60으로 나누면 KPS입니다. 번갈아 치면 한 손가락은 두 타마다 한 번 치므로, 이 값으로 같은 손가락을 다시 칠 수 있는 빠르기를 정합니다. 예를 들어 961 BPM이면 16.0 KPS이고, 같은 손가락 간격은 약 125 ms입니다.

`테스트`를 누르고 두 키를 번갈아 5초 동안 최대한 빠르게 치면 값이 바로 적용됩니다. 시간은 첫 입력부터 재고, 처음 누른 두 키만 셉니다. 직접 정하려면 옆의 슬라이더를 움직이거나 `-`, `+`로 1 BPM씩 맞춥니다.

테스트는 메인 메뉴에서만 됩니다. 게임은 UnityModManager 창이 열려 있어도 키 입력을 처리하므로, 에디터에서는 연타가 타일을 고칠 수 있고 커스텀 레벨 선택에서는 레벨이 시작될 수 있기 때문입니다. 테스트하는 동안에는 행성이 키에 반응하지 않고, 끝나면 바로 다시 반응합니다. 그래도 화면이 바뀌면 테스트는 취소됩니다. 테스트 중 누른 키는 실제 입력이므로 키 뷰어에 켜지고 Jipper 키 통계에도 더해집니다.

## 타법

세계 1위 [Jipper](https://www.youtube.com/@jipper1214)의 키뷰어를 영상에서 프레임 단위로 읽어 얻은 실제 타법을 기준으로 만들었습니다. 사람은 음마다 손가락을 고르지 않고 손 단위로 묶어서 칩니다. 그래서 입력을 **왼손 묶음과 오른손 묶음**으로 나눕니다. 묶음의 손 모양은 음 수로 정해지므로, 반복되는 리듬에는 대체로 같은 모양이 나와 외워서 따라 치기 쉽습니다.

**빠르기:** 빠를수록 손가락을 늘립니다. 아래 표는 고른 연타를 왼손 주손, 밖안으로 계산한 값입니다. BPM은 1분에 누르는 횟수이고 20 BPM 단위로 쟀습니다.

| 기반, 2키 기반 최대 속도 | `F` | `F J` | `D F K J` | `A S D F ; L K J` |
|---|---|---|---|---|
| 4키기반, 1120 BPM | 280까지 | 300-580 | 600-1120 | 1140부터 |
| 2키기반, 1120 BPM | 400까지 | 420-820 | 840-2020 | 2040부터 |
| 4키기반, 798 BPM | 180까지 | 200-580 | 600-780 | 800부터 |
| 2키기반, 798 BPM | 280까지 | 300-580 | 600-1440 | 1460부터 |

두 기반의 차이는 지퍼의 영상에서 그대로 나옵니다. 같은 곡을 44 ms 간격의 4키기반에서는 8손가락으로, 37 ms 간격의 2키기반에서는 `D F K J`로 쳤습니다.

**롤 방식**은 묶음을 나누는 데 영향을 주지 않고, 묶음 안의 방향만 정합니다. 오른손이 주손인 4키 기준입니다.

| 롤 방식 | 순서 |
|---|---|
| 안밖 | `J K F D` |
| 밖안 | `K J D F` |
| 계단 | `J K D F` |
| 역계단 | `K J F D` |

한 손 묶음이 간격 30 ms보다 좁은 두 음뿐이면 롤 방식과 상관없이 검지부터 칩니다.

**동타:** 간격 12 ms 이내, 전체 폭 35 ms 이내의 입력은 동타로 보고 한 번에 누릅니다. 2~4동타는 양손에 나누지 않고 한 손에 담아 손을 번갈아 갑니다(`[D F S] [J K L] [D F] [J K]`). 느린 동타는 같은 손으로 이어질 수 있고, 엄지 키가 있으면 빠른 2동타를 한 손에서 이어 칠 수 있습니다(`[D S] [엄지 F]`). 엄지 키가 없으면 번갈아 쳐도 못 치는 빠르기에서만 바깥 손가락까지 돌립니다(`[A S] [D F] [; L] [K J]`). 5동타는 엄지가 있으면 한 손, 없으면 3과 2로 나눕니다. 6동타는 양손 셋씩, 8동타는 8손가락이고, 9동타부터 주손 엄지가 더해집니다. 8동타는 최소 간격보다 조금 빠르게 반복돼도 모양을 돌려 쓰지 않습니다.

**아래 키:** 엄지와 손바닥 키는 동타를 돕거나, 8손가락을 다 돌려도 실제로 못 치는 연타에서만 씁니다. 아예 쓰지 않으려면 최대 사용 키를 8로 둡니다.

**지퍼와 얼마나 같은가:** 같은 맵의 지퍼 플레이와 비교한 값입니다. 동타는 한 칸으로 묶어 키가 모두 같아야 맞다고 셉니다. 지퍼의 설정(왼손 주손, 밖안, 1120 BPM)으로 계산했습니다. 반복 일관성은 간격이 같은 6타가 가장 흔한 키 순서로 나온 비율이고, 괄호는 지퍼 본인의 값입니다.

| 맵 | 입력 수 | 키 일치 | 손 일치 | 반복 일관성 |
|---|---|---|---|---|
| First Town Of This Journey, 풀 4키기반 | 1,875 | 91.9% | 97.9% | 69.2% (66.4%) |
| First Town Of This Journey 1.2배속, 풀 2키기반 | 1,875 | 93.7% | 98.1% | 86.3% (71.3%) |
| REX, Pure Perfect, 2키기반 + 양손 동타 | 7,040 | 70.2% | 84.7% | 58.2% (34.9%) |

## 키 역할과 상태 줄

키 역할은 뷰어 배열 순서에서 추정한 값입니다. 어떤 뷰어에도 손가락 정보가 없기 때문입니다. 실제 손 배치와 다르면 키 역할 편집에서 고칩니다. 발 키는 쓰지 않습니다.

맨 아래 상태 줄에 연결된 뷰어, 배치 기준, 키 수, 마지막 계획의 최대 속도 초과 횟수가 나옵니다. 초과는 같은 손가락을 최대 속도의 약 1.33배보다 빠르게 다시 친 횟수입니다.

## 지원하는 뷰어

연결된 뷰어 전부에 같은 입력을 보냅니다. 배치 기준은 Jipper, LineKeyViewer, AdofaiTweaks 순서입니다. 뷰어 모드에 대한 컴파일 시점 의존은 없고, 시도가 시작될 때마다 다시 찾습니다. 하나도 연결되지 않으면 계산도 표시도 하지 않습니다.

- JipperResourcePack 1.6.0, 1.4.9
- LineKeyViewer 1.0.2
- AdofaiTweaks 2.9.2, 2.9.3의 키 뷰어

Jipper의 평생 키 통계에는 자동 재생 입력이 남지 않습니다. 시도 중에는 키별 숫자, 합계, KPS, 레인이 실제 입력처럼 움직이고, 시도가 끝나면 올라간 숫자만큼 빼고 저장합니다.

## 검증한 환경

Windows Sandbox 안의 Mods 폴더에 1.2.0 zip의 파일을 복사하고, 테스트용 모드가 자동 재생을 켜서 음악 없는 레벨을 기본 설정과 60 fps로 돌렸습니다. 실제 맵으로는 REX(Camellia)를 밖안, 오른손 주손, 2키기반, 798 BPM, 최대 16키로 끝까지 돌렸습니다. UnityModManager 설치기, Otto 버튼, 안밖·밖안 외의 롤 방식, 소리는 게임 안에서 확인하지 않았습니다. 롤 방식은 단위 테스트로 검증했습니다.

| 게임 | 키 뷰어 | 경로 | 결과 |
|---|---|---|---|
| R150 | Jipper 1.6.0 + LineKeyViewer 1.0.2 | 에디터 2회, 일반 1회 | 계획한 126번과 켠 순서 일치, 키 통계 복원 |
| R150 | Jipper 1.6.0 + LineKeyViewer 1.0.2 | REX 전체 1회 | 7,040타 모두 켜짐, 키별 횟수 일치, 키 통계 복원, 아래 키 404회, 7,040번 중 6,991번이 4프레임(약 67 ms) 켜짐, 100 ms를 넘은 12번은 홀드 타일, 없어진 항목이 있는 예전 설정 파일을 그대로 읽음 |
| R150 | AdofaiTweaks 2.9.3 | 에디터 2회 | 126번 모두 켜짐, 1회는 프레임이 밀려 한 쌍의 순서가 바뀜 |
| R150 | 없음 | 에디터 2회 | 입력 0회, 모드 오류 없음 |
| R150 | 없음, 자동 재생 끔 + 자동 타일 | 일반 1회 (의도한 실패로 종료) | 키 점등 0회 |
| R145 | Jipper 1.4.9 | 에디터 2회 | 126번 일치, 키 통계 복원 |

2키 기반 최대 속도 테스트는 R150과 R145의 메인 메뉴에서 Windows 키 입력과 마우스 클릭을 넣어 확인했습니다. 두 키를 720, 960, 1,200 BPM으로 번갈아 넣으면 각각 약 720, 962, 1,200 BPM으로 측정됐습니다. 측정 중에는 키 76번에 행성이 한 번도 반응하지 않았고, 끝나면 입력이 돌아왔습니다. 한 키만 넣으면 값이 바뀌지 않았습니다. 취소, 창 닫기, 측정 중 화면 전환, 에디터에서의 시작 거부, `테스트`, `-`, `+`, 슬라이더 클릭도 확인했습니다. 사람이 키보드로 치는 경우는 확인하지 못했습니다.

REX는 아직 켜져 있는 키를 다시 누를 때 한 프레임 뒤로 밀리므로 켠 순서가 계획과 일부 다릅니다(7,040자리 중 14자리). LineKeyViewer 쪽 점등 횟수는 REX에서 7,040번 중 18번 적게 집계됐고, 같은 실행에서 Jipper는 전부 켜졌습니다. 한 프레임만 켜진 입력도 18번이라 폴링 뷰어가 놓친 것으로 보이지만 확인하지는 못했습니다. 다른 프로그램이 CPU를 함께 쓰면 프레임이 떨어져 입력이 합쳐질 수 있습니다. 부하를 준 실행에서 REX가 7,024타로 줄었습니다.

## 알려진 제한

- 키는 행성이 착지하는 프레임에 켜집니다. 60 fps에서 계산된 타격 시각보다 보통 20-25 ms, 최대 68 ms 늦었고, 입력 오프셋만큼 타격음과도 어긋날 수 있습니다.
- 다중탭 타일은 게임 안에서 확인하지 못했습니다. 테스트 레벨의 Multitap 이벤트가 적용되지 않아 단위 테스트로만 검증했습니다.
- 큰 레벨은 계산이 끝나기 전까지 키가 켜지지 않습니다. 계산은 백그라운드에서 돌고 게임을 멈추지 않습니다.
- 도중에 에디터에서 Otto를 켜면 다음 착지부터 계산을 시작합니다.
- 연습 모드처럼 포털이 앞당겨지면 계획의 뒷부분은 쓰이지 않습니다.
- 시도 도중 게임이 꺼지면(창을 닫는 경우 포함) Jipper 키 통계가 복원되지 않을 수 있습니다. Jipper 1.4.9는 자체 스레드가 저장하므로 이 가능성이 더 큽니다.
- AdofaiTweaks 키 뷰어는 자동 재생 중 실제 키 입력을 표시하지 않고, 코드 4096 이상의 키를 표시하지 못합니다.
- Jipper 1.4.9는 자체 스레드가 키를 읽으므로, 같은 키를 실제로 누르고 있거나 스레드가 입력을 놓치면 통계가 한 번 더 빠질 수 있습니다.
- 타법 값은 지퍼 영상의 맵 2곡에 맞췄습니다. 다른 성격의 맵에서는 일치도가 낮을 수 있습니다. He He He처럼 지퍼가 16키기반으로 친 영상은 기준에 넣지 않았고, 그런 타법은 만들지 않습니다.
- 아주 빠른 구간에서는 최대 속도 초과가 생길 수 있습니다. 상태 줄에 초과 횟수가 나옵니다.
- 묶음은 밖안 순서를 기준으로 나눕니다. 안밖과 계단 계열은 아주 빠른 구간에서 같은 손가락 간격이 더 짧아질 수 있습니다. REX를 최대 8키, 4키기반, 1120 BPM으로 계산하면 최대 속도 초과가 밖안 55회, 안밖 87회입니다.
- 2키 기반 최대 속도는 두 손가락으로 재서 모든 손가락에 같게 적용합니다. 약지나 소지가 유독 느려도 따로 낮추지 않습니다.
- 게임을 켜자마자 레벨을 열면 JALib 기반 뷰어가 아직 로드되지 않아 그 시도에는 연결되지 않을 수 있습니다. 다음 시도부터 다시 찾습니다.

## 빌드

.NET SDK 10이 필요합니다. 테스트는 게임 없이 돌아갑니다. 빌드에는 UnityModManager가 설치된 게임이 필요합니다.

```powershell
dotnet build src/AutoFingering.csproj -c Release -p:GameDir=<게임 폴더>
```

배포용 zip은 구버전과 신버전 게임(R145, R150)으로 빌드해 IL이 같은지 비교한 뒤 만듭니다. ilspycmd는 `dotnet tool install -g ilspycmd`로 설치합니다.

```powershell
./pack.ps1 -IlSpy <ilspycmd 경로>
```

게임 경로가 다르면 `-GameOld`와 `-GameNew`로 지정합니다. 결과는 `dist/AutoFingering-<버전>.zip`입니다. 테스트는 `dotnet run --project tests/AutoFingering.Tests.csproj -c Release`입니다.

코드 주석의 `D:123`, `F:123`은 디컴파일한 게임 소스의 줄 번호입니다. D는 게임 릴리스 R150, F는 R145입니다.

`ponytail:` 주석은 일부러 단순하게 둔 부분과 그 한계를 표시합니다.

라이선스는 [MIT](LICENSE)입니다.

---

<a id="english"></a>

# Auto Fingering (English)

A UnityModManager mod for A Dance of Fire and Ice. While the game's autoplay (Otto) runs, it computes a humanly playable fingering for the whole level and lights the assigned key in your key viewer at the moment autoplay hits each tile. It has no viewer of its own, so you need a key viewer mod.

It only displays. It sends no input and never changes `RDC.auto`, so it has no effect on judgement or results.

## Install

1. You need the game with [UnityModManager](https://github.com/newman55/unity-mod-manager) installed, and one of the [supported key viewers](#supported-viewers).
2. Download `AutoFingering-<version>.zip` from the [releases page](https://github.com/newmouse11/ADOFAI-AutoPlay-Keyviewer-/releases/latest) and install it through UnityModManager, or unpack it so `Info.json` and `AutoFingering.dll` sit in `<game folder>/Mods/AutoFingering/`.
3. Start the game. The settings window opens with Ctrl+F10 by default.

## Usage

1. First pick a roll style under `Auto Fingering` in the settings window. The fingering is computed from these settings when autoplay starts.
2. Open a level in the level editor. In the retail game, autoplay is effectively only available through the editor's Otto button (shortcut A, or Alt+A while a tile is selected).
3. Turn Otto on and play. Keys start lighting as soon as the solve finishes. Restarting with the same level and settings does not solve again.

## Settings

| Setting | Values | Default |
|---|---|---|
| Language | Follow game / Korean / English | Follow game |
| Roll style | Outward (안밖) / Inward (밖안) / Stair (계단) / Reverse stair (역계단) | Outward |
| Main hand | Right / Left | Right |
| Base | 2-key / 4-key | 2-key |
| Max keys used | 4, 6, 8, 10, 12, 16, All (limited by the viewer's key count) | All |
| 2-key max speed | Test, slider or `-`/`+` buttons, 480-2000 BPM | 923 BPM |
| Key role editor | Hand and finger per slot, or not used. Saved per key count | Inferred from viewer array order |

2-key max speed is how many presses per minute (BPM) you can reach alternating two keys. It matches CBPM on the Jipper overlay, and dividing by 60 gives KPS. Alternating means each finger plays every second press, so this value sets how fast the same finger may repeat. For example, 961 BPM is 16.0 KPS, which means about 125 ms between presses of one finger.

Press `Test` and alternate two keys as fast as you can for 5 seconds, and the value applies right away. Timing starts at your first press, and only the first two keys you press count. To set it yourself, move the slider next to the button, or use `-` and `+` for steps of 1 BPM.

The test only runs from the main menu. The game keeps handling keys while the UnityModManager window is open, so mashing could edit tiles in the editor or start a level in custom level select. The planet ignores keys during the test and responds again as soon as it ends, and the test stops if the screen changes anyway. Keys pressed during the test are real input, so they light your key viewer and add to Jipper's key stats.

## Fingering

The fingering follows real play by world rank 1 [Jipper](https://www.youtube.com/@jipper1214), read frame by frame from the key viewer in his videos. People do not choose a finger per note, they play in groups per hand. So input is split into **left hand runs and right hand runs**. The shape of a run follows from its note count, so a repeated rhythm mostly gets the same shape and is easier to memorize.

**Speed:** faster input uses more fingers. The table shows even streams computed with main hand left, Inward. BPM means presses per minute, measured in steps of 20 BPM.

| Base, 2-key max speed | `F` | `F J` | `D F K J` | `A S D F ; L K J` |
|---|---|---|---|---|
| 4-key, 1120 BPM | up to 280 | 300-580 | 600-1120 | from 1140 |
| 2-key, 1120 BPM | up to 400 | 420-820 | 840-2020 | from 2040 |
| 4-key, 798 BPM | up to 180 | 200-580 | 600-780 | from 800 |
| 2-key, 798 BPM | up to 280 | 300-580 | 600-1440 | from 1460 |

The difference between the two bases comes straight from Jipper's videos. He played the same song with eight fingers on 4-key base at 44 ms gaps, and with `D F K J` on 2-key base at 37 ms gaps.

**Roll style** does not affect how runs are split, only the direction inside a run. Shown for 4 keys with the right hand as main hand.

| Roll style | Order |
|---|---|
| Outward (안밖) | `J K F D` |
| Inward (밖안) | `K J D F` |
| Stair (계단) | `J K D F` |
| Reverse stair (역계단) | `K J F D` |

When a hand run is just two notes less than 30 ms apart, it starts from the index finger, whatever the roll style.

**Chords:** input with gaps within 12 ms, 35 ms wide at most, counts as a chord and is pressed at once. Chords of 2 to 4 notes stay in one hand and hands alternate (`[D F S] [J K L] [D F] [J K]`). Slow chords can stay on the same hand, and with a thumb key, fast 2-note chords can continue in one hand (`[D S] [thumb F]`). Without a thumb key, outer fingers join only at speeds where alternating hands cannot be played (`[A S] [D F] [; L] [K J]`). A 5-note chord stays in one hand with a thumb, and splits into 3 and 2 without one. A 6-note chord is three per hand, an 8-note chord uses eight fingers, and the main thumb joins at 9 notes. An 8-note chord keeps its shape even when it repeats slightly faster than the minimum interval.

**Lower keys:** thumb and palm keys only help chords, or streams that eight fingers truly cannot play. To never use them, set max keys to 8.

**How close to Jipper:** compared with his play on the same chart, slot by slot, where a chord counts as one slot and all its keys must match, using his settings (main hand left, Inward, 1120 BPM). Repeat consistency is how often 6 presses with the same gaps come out in their most common key order, with Jipper's own value in parentheses.

| Chart | Presses | Key match | Hand match | Repeat consistency |
|---|---|---|---|---|
| First Town Of This Journey, full 4-key | 1,875 | 91.9% | 97.9% | 69.2% (66.4%) |
| First Town Of This Journey 1.2x, full 2-key | 1,875 | 93.7% | 98.1% | 86.3% (71.3%) |
| REX, Pure Perfect, 2-key plus two-hand chords | 7,040 | 70.2% | 84.7% | 58.2% (34.9%) |

## Key roles and status line

Key roles are inferred from the viewer's array order, because no viewer records which finger plays a key. If they differ from your hands, change them in the key role editor. Foot keys are never used.

The status line at the bottom shows the bound viewers, the layout source, the key count and how often the last plan went over max speed. That counts re-strikes of the same finger more than about 1.33 times faster than max speed.

## Supported viewers

All bound viewers receive the same input. Layout authority is Jipper, then LineKeyViewer, then AdofaiTweaks. There is no compile-time dependency on any viewer mod, and they are looked up again at each attempt. If none is bound, nothing is computed or shown.

- JipperResourcePack 1.6.0, 1.4.9
- LineKeyViewer 1.0.2
- AdofaiTweaks 2.9.2, 2.9.3 key viewer

Autoplay presses do not stay in Jipper's lifetime key stats. During an attempt the per-key counts, the total, KPS and rain move like real input, and when the attempt ends the added counts are subtracted and saved.

## Tested setups

The files from the 1.2.0 zip were copied into the Mods folder inside Windows Sandbox, and a test mod turned autoplay on and ran a no-music level at default settings and 60 fps. As a real chart, REX (Camellia) was run to the end with Inward, main hand right, 2-key base, 798 BPM and max 16 keys. The UnityModManager installer, the Otto button, roll styles other than Outward and Inward, and sound were not checked in game. Roll styles are covered by unit tests.

| Game | Key viewer | Route | Result |
|---|---|---|---|
| R150 | Jipper 1.6.0 + LineKeyViewer 1.0.2 | Editor twice, normal once | All 126 planned presses fired in order, key stats restored |
| R150 | Jipper 1.6.0 + LineKeyViewer 1.0.2 | Full REX once | All 7,040 presses lit, per-key counts matched, key stats restored, lower keys 404 times, 6,991 of 7,040 lit for 4 frames (about 67 ms), the 12 over 100 ms were hold tiles, an old settings file with removed options loaded as is |
| R150 | AdofaiTweaks 2.9.3 | Editor twice | All 126 lit, one run swapped one pair when a frame slipped |
| R150 | None | Editor twice | No input, no mod errors |
| R150 | None, autoplay off with auto tiles | Normal once (ends in a deliberate fail) | No keys lit |
| R145 | Jipper 1.4.9 | Editor twice | All 126 matched, key stats restored |

The 2-key max speed test was checked from the main menu on R150 and R145 with injected Windows key input and mouse clicks. Alternating two keys at 720, 960 and 1,200 BPM measured about 720, 962 and 1,200 BPM. During a test the planet did not respond once in 76 presses, and input came back afterwards. One key alone left the value unchanged. Cancelling, closing the window, a screen change during the test, the refusal in the editor, and clicks on `Test`, `-`, `+` and the slider were also checked. A person playing on a keyboard was not checked.

On REX a key that is still lit is re-pressed one frame later, so the lit order partly differs from the plan (14 of 7,040 positions). LineKeyViewer's lit count came out 18 short of 7,040 on REX, while Jipper lit all of them in the same run. Exactly 18 presses stayed lit for one frame, so the polling viewer probably missed them, but this was not confirmed. Other programs using the CPU can drop frames and merge presses. A run under load reduced REX to 7,024 presses.

## Known limits

- Keys light on the frame the planet lands. At 60 fps they were usually 20-25 ms after the computed hit time, 68 ms at most, and they can also differ from the hit sound by your input offset.
- Multitap tiles were not confirmed in game. The Multitap event in the test level did not apply, so they are covered by unit tests only.
- On large levels no key lights until the solve finishes. The solve runs in the background and does not stall the game.
- Turning Otto on in the middle of an editor run starts the solve at the next landing.
- When the portal is moved earlier, as in practice mode, the rest of the plan goes unused.
- If the game closes during an attempt, even through a normal window close, Jipper's key stats may not be restored. This is more likely on Jipper 1.4.9, whose own thread saves them.
- The AdofaiTweaks key viewer does not show real key presses during autoplay, and cannot show key codes of 4096 or above.
- Jipper 1.4.9 reads keys on its own thread, so physically holding the same key, or the thread missing a press, can subtract one count too many.
- The fingering values were fitted to two charts from Jipper's videos. Charts of a different character may match less. Videos he played on 16-key base, such as He He He, were left out, and that style is not produced.
- Very fast sections can go over max speed. The status line shows the count.
- Runs are split using the Inward order. Outward and the stair styles can repeat a finger sooner in very fast sections. REX at max 8 keys, 4-key base, 1120 BPM goes over max speed 55 times with Inward and 87 times with Outward.
- 2-key max speed is measured with two fingers and applied to all of them. It is not lowered for a slower ring finger or pinky.
- Opening a level right after starting the game can miss JALib-based viewers that have not loaded yet, for that attempt only. They are looked up again on the next attempt.

## Build

Requires .NET SDK 10. The tests run without the game. Building needs the game with UnityModManager installed.

```powershell
dotnet build src/AutoFingering.csproj -c Release -p:GameDir=<game folder>
```

The release zip is built against an older and a newer game (R145 and R150), comparing the IL. Install ilspycmd with `dotnet tool install -g ilspycmd`.

```powershell
./pack.ps1 -IlSpy <path to ilspycmd>
```

Use `-GameOld` and `-GameNew` if your game paths differ. The output is `dist/AutoFingering-<version>.zip`. Run the tests with `dotnet run --project tests/AutoFingering.Tests.csproj -c Release`.

`D:123` and `F:123` in code comments are line numbers in the decompiled game source. D is game release R150 and F is R145.

`ponytail:` comments mark deliberate simplifications and their limits.

Licensed under [MIT](LICENSE).

# Auto Fingering

[English](#english)

A Dance of Fire and Ice용 UnityModManager 모드입니다. 게임의 자동 재생(Otto)이 돌 때 레벨 전체의 운지를 사람이 칠 수 있는 형태로 계산하고, 자동 재생이 타일을 치는 순간 배정된 키를 키 뷰어에 켜 줍니다. 쓸 수 있는 키 뷰어가 없으면 자체 뷰어를 보여 줍니다.

표시만 하는 모드입니다. 입력을 보내지 않고 `RDC.auto`도 바꾸지 않으므로 판정과 결과에는 영향이 없습니다.

## 설치

1. [UnityModManager](https://github.com/newman55/unity-mod-manager)가 설치된 게임이 필요합니다.
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
| 같은 손가락 최소 간격 | 60-250 ms | 130 ms |
| 3연타 나누기 | 주손 두 손가락 / 부손 두 손가락 | 주손 두 손가락 |
| 키 역할 편집 | 슬롯마다 손과 손가락, 또는 사용 안 함. 키 개수별로 저장 | 뷰어 배열 순서로 추정 |
| 내장 뷰어 | 키 프리셋 4/8/12/16, X, Y, 크기 | 8키 |

롤 방식별 순서는 다음과 같습니다. 오른손이 주손인 4키 `D F J K` 기준입니다.

| 롤 방식 | 순서 |
|---|---|
| 안밖 | `J K F D` |
| 밖안 | `K J D F` |
| 계단 | `J K D F` |
| 역계단 | `K J F D` |

키 역할은 뷰어 배열 순서에서 추정한 값입니다. 어떤 뷰어에도 손가락 정보가 없기 때문입니다. 실제 손 배치와 다르면 키 역할 편집에서 고칩니다. 발 키는 쓰지 않습니다.

맨 아래 상태 줄에 연결된 뷰어, 배치 기준, 키 수, 마지막 계획의 최소 간격 위반 수가 나옵니다.

## 지원하는 뷰어

연결된 뷰어 전부에 같은 입력을 보냅니다. 배치 기준은 Jipper, LineKeyViewer, AdofaiTweaks, 내장 뷰어 순서입니다. 뷰어 모드에 대한 컴파일 시점 의존은 없고, 시도가 시작될 때마다 다시 찾습니다.

- JipperResourcePack 1.6.0, 1.4.9
- LineKeyViewer 1.0.2
- AdofaiTweaks 2.9.2, 2.9.3의 키 뷰어
- 내장 뷰어: 위 뷰어가 하나도 연결되지 않았을 때만 보입니다.

Jipper의 평생 키 통계에는 자동 재생 입력이 남지 않습니다. 시도 중에는 키별 숫자, 합계, KPS, 레인이 실제 입력처럼 움직이고, 시도가 끝나면 올라간 숫자만큼 빼고 저장합니다.

## 검증한 환경

Windows Sandbox 안의 Mods 폴더에 1.0.0 zip의 파일을 복사하고, 테스트용 모드가 자동 재생을 켜서 음악 없는 레벨을 기본 설정과 60 fps로 돌렸습니다. UnityModManager 설치기, Otto 버튼, 기본값이 아닌 롤 방식, 소리는 게임 안에서 확인하지 않았습니다. 롤 방식은 단위 테스트로 검증했습니다.

| 게임 | 키 뷰어 | 경로 | 결과 |
|---|---|---|---|
| R150 | Jipper 1.6.0 + LineKeyViewer 1.0.2 | 에디터 2회, 일반 1회 | 계획한 126번과 켠 순서 일치, 키 통계 복원 |
| R150 | AdofaiTweaks 2.9.3 | 에디터 2회 | 126번 일치 |
| R150 | 없음(내장 뷰어) | 에디터 2회 | 126번 일치 |
| R150 | 없음, 자동 재생 끔 + 자동 타일 | 일반 1회 (의도한 실패로 종료) | 키 점등 0회 |
| R145 | Jipper 1.4.9 | 에디터 2회 | 126번 일치, 키 통계 복원 |

LineKeyViewer 쪽 점등 횟수가 한 번의 실행에서 126번 중 1번 적게 집계된 적이 있습니다. 같은 실행에서 Jipper는 126번이었습니다.

## 알려진 제한

- 키는 행성이 착지하는 프레임에 켜집니다. 60 fps에서 계산된 타격 시각보다 보통 20-25 ms, 최대 68 ms 늦었고, 입력 오프셋만큼 타격음과도 어긋날 수 있습니다.
- 다중탭 타일은 게임 안에서 확인하지 못했습니다. 테스트 레벨의 Multitap 이벤트가 적용되지 않아 단위 테스트로만 검증했습니다.
- 큰 레벨은 계산이 끝나기 전까지 키가 켜지지 않습니다. 계산은 백그라운드에서 돌고 게임을 멈추지 않습니다.
- 도중에 에디터에서 Otto를 켜면 다음 착지부터 계산을 시작합니다.
- 연습 모드처럼 포털이 앞당겨지면 계획의 뒷부분은 쓰이지 않습니다.
- 시도 도중 게임이 꺼지면(창을 닫는 경우 포함) Jipper 키 통계가 복원되지 않을 수 있습니다. Jipper 1.4.9는 자체 스레드가 저장하므로 이 가능성이 더 큽니다.
- AdofaiTweaks 키 뷰어는 자동 재생 중 실제 키 입력을 표시하지 않고, 코드 4096 이상의 키를 표시하지 못합니다.
- Jipper 1.4.9는 자체 스레드가 키를 읽으므로, 같은 키를 실제로 누르고 있거나 스레드가 입력을 놓치면 통계가 한 번 더 빠질 수 있습니다.
- 쉼 없이 빨라지는 구간에서는 최소 간격 위반이 드물게 생길 수 있습니다. 섞은 연타 100만 타에서 수십 번이었습니다. 상태 줄에 위반 수가 나옵니다.
- 16키 계단은 초당 100타 이상에서 표 순서로 안정될 때까지 수 초 걸릴 수 있습니다.
- 한 손만 쓰거나 검지를 끈 키 역할에서는 간격 0.4초 이상의 느린 타일이 한 키로 몰릴 수 있습니다.

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

라이선스는 아직 정하지 않았습니다.

---

<a id="english"></a>

# Auto Fingering (English)

A UnityModManager mod for A Dance of Fire and Ice. While the game's autoplay (Otto) runs, it computes a humanly playable fingering for the whole level and lights the assigned key in your key viewer at the moment autoplay hits each tile. If no key viewer is available, it shows its own.

It only displays. It sends no input and never changes `RDC.auto`, so it has no effect on judgement or results.

## Install

1. You need the game with [UnityModManager](https://github.com/newman55/unity-mod-manager) installed.
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
| Same-finger minimum interval | 60-250 ms | 130 ms |
| Triplet split | Main hand two fingers / Off hand two fingers | Main hand two fingers |
| Key role editor | Hand and finger per slot, or not used. Saved per key count | Inferred from viewer array order |
| Built-in viewer | Key preset 4/8/12/16, X, Y, scale | 8 keys |

Each roll style uses this order, shown for 4 keys `D F J K` with the right hand as main hand.

| Roll style | Order |
|---|---|
| Outward (안밖) | `J K F D` |
| Inward (밖안) | `K J D F` |
| Stair (계단) | `J K D F` |
| Reverse stair (역계단) | `K J F D` |

Key roles are inferred from the viewer's array order, because no viewer records which finger plays a key. If they differ from your hands, change them in the key role editor. Foot keys are never used.

The status line at the bottom shows the bound viewers, the layout source, the key count and the floor violations of the last plan.

## Supported viewers

All bound viewers receive the same input. Layout authority is Jipper, then LineKeyViewer, then AdofaiTweaks, then the built-in viewer. There is no compile-time dependency on any viewer mod, and they are looked up again at each attempt.

- JipperResourcePack 1.6.0, 1.4.9
- LineKeyViewer 1.0.2
- AdofaiTweaks 2.9.2, 2.9.3 key viewer
- Built-in viewer: only shown when none of the above is bound.

Autoplay presses do not stay in Jipper's lifetime key stats. During an attempt the per-key counts, the total, KPS and rain move like real input, and when the attempt ends the added counts are subtracted and saved.

## Tested setups

The files from the 1.0.0 zip were copied into the Mods folder inside Windows Sandbox, and a test mod turned autoplay on and ran a no-music level at default settings and 60 fps. The UnityModManager installer, the Otto button, non-default roll styles and sound were not checked in game. Roll styles are covered by unit tests.

| Game | Key viewer | Route | Result |
|---|---|---|---|
| R150 | Jipper 1.6.0 + LineKeyViewer 1.0.2 | Editor twice, normal once | All 126 planned presses fired in order, key stats restored |
| R150 | AdofaiTweaks 2.9.3 | Editor twice | All 126 matched |
| R150 | None (built-in viewer) | Editor twice | All 126 matched |
| R150 | None, autoplay off with auto tiles | Normal once (ends in a deliberate fail) | No keys lit |
| R145 | Jipper 1.4.9 | Editor twice | All 126 matched, key stats restored |

In one run LineKeyViewer's lit count came out 1 short of 126. Jipper showed 126 in that same run.

## Known limits

- Keys light on the frame the planet lands. At 60 fps they were usually 20-25 ms after the computed hit time, 68 ms at most, and they can also differ from the hit sound by your input offset.
- Multitap tiles were not confirmed in game. The Multitap event in the test level did not apply, so they are covered by unit tests only.
- On large levels no key lights until the solve finishes. The solve runs in the background and does not stall the game.
- Turning Otto on in the middle of an editor run starts the solve at the next landing.
- When the portal is moved earlier, as in practice mode, the rest of the plan goes unused.
- If the game closes during an attempt, even through a normal window close, Jipper's key stats may not be restored. This is more likely on Jipper 1.4.9, whose own thread saves them.
- The AdofaiTweaks key viewer does not show real key presses during autoplay, and cannot show key codes of 4096 or above.
- Jipper 1.4.9 reads keys on its own thread, so physically holding the same key, or the thread missing a press, can subtract one count too many.
- Sections that keep speeding up with no pause can rarely break the minimum interval: dozens of times in a million mixed presses. The status line shows the count.
- 16-key stair can take a few seconds to settle into table order at 100 or more presses per second.
- With one hand only or an index finger turned off in the key roles, slow tiles 0.4 s or more apart can all land on one key.

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

No license has been chosen yet.

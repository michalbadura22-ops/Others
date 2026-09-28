# VPN Pinger

Lekka aplikacja okienkowa dla Windows, która cyklicznie monitoruje dostępność Internetu oraz zasobu dostępnego przez VPN.

Program pomaga ustalić, czy chwilowe problemy z dostępem do zasobów firmowych wynikają z zerwania tunelu VPN, czy z utraty całego połączenia internetowego.

## Funkcje

- niezależne monitorowanie dwóch adresów:
  - publicznego adresu internetowego,
  - adresu dostępnego przez VPN;
- równoległe wykonywanie obu testów;
- konfigurowalny interwał oraz timeout;
- niewielkie okno zawsze widoczne na wierzchu;
- kolorystyczna informacja o stanie połączeń;
- zapisywanie utraty i odzyskania połączenia do pliku tekstowego;
- zapis stanu obu połączeń przy każdym wykrytym zdarzeniu;
- brak instalatora i dodatkowej konfiguracji.

## Przykładowy widok

Internet   8.8.8.8         OK    15 ms
VPN        10.0.0.1        OK    12 ms

W przypadku problemu tło okna zmienia kolor, a szczegóły zdarzenia zostają zapisane w logu.

## Konfiguracja monitorowanych adresów

Drugi adres należy zmienić na stabilny host osiągalny wyłącznie przez VPN, na przykład:

- wewnętrzny serwer DNS;
- serwer aplikacyjny;
- bramę lub inne urządzenie odpowiadające na ICMP;
- dowolny stale dostępny adres w sieci firmowej.

Najlepiej nie używać komputera użytkownika ani serwera, który może być okresowo wyłączany.

## Interwał i timeout

Domyślny interwał między testami wynosi 5 sekund:
Timeout pojedynczego zapytania wynosi 2 sekundy:
Dwa adresy testowane co 5 sekund generują niewielki ruch i nie powinny zauważalnie obciążać sieci ani bramy VPN.

## Logowanie

Log jest zapisywany obok pliku wykonywalnego jako:
VpnMonitor.log
Program zapisuje tylko zmianę stanu, a nie każdy poprawny ping.
Przykład utraty wyłącznie połączenia VPN:
2026-09-28 11:38:15.381;VPN;10.0.0.1;PRZERWANIE;TimedOut;Internet=OK(15ms);VPN=BRAK(TimedOut)
Przykład jednoczesnej utraty Internetu i VPN:
2026-09-28 11:39:20.114;Internet;8.8.8.8;PRZERWANIE;TimedOut;Internet=BRAK(TimedOut);VPN=BRAK(TimedOut)
2026-09-28 11:39:20.115;VPN;10.0.0.1;PRZERWANIE;TimedOut;Internet=BRAK(TimedOut);VPN=BRAK(TimedOut)
Przykład odzyskania połączenia VPN:
2026-09-28 11:39:30.221;VPN;10.0.0.1;ODZYSKANO;ping=12 ms;Internet=OK(15ms);VPN=OK(12ms)

## Interpretacja wyników

- `Internet=OK`, `VPN=BRAK` oznacza, że połączenie internetowe nadal działa, ale tunel VPN lub trasa do sieci firmowej jest niedostępna.
- `Internet=BRAK`, `VPN=BRAK` wskazuje na problem z lokalnym połączeniem, routerem, operatorem albo kartą sieciową.
- `Internet=BRAK`, `VPN=OK` może oznaczać pojedynczy brak odpowiedzi ICMP od publicznego hosta. Warto porównać wynik z kolejnymi pomiarami.
- `Internet=OK`, `VPN=OK` oznacza prawidłowe działanie obu połączeń.

## Wymagania

Do uruchomienia wersji zależnej od środowiska wymagane jest:

- Windows 10 lub nowszy;
- .NET Desktop Runtime 8.

Wersja opublikowana jako `self-contained` nie wymaga osobnej instalacji środowiska .NET, ale ma większy rozmiar.

- Monitor korzysta z ICMP. Wybrany host musi odpowiadać na ping.
- Brak odpowiedzi nie zawsze oznacza całkowity brak łączności. Urządzenie lub firewall może blokować albo ograniczać ICMP.
- Publiczny adres kontrolny powinien być kierowany poza tunel VPN, jeśli celem jest niezależne rozróżnienie awarii Internetu od awarii VPN.
- Dla najbardziej miarodajnego wyniku jako cel VPN należy wybrać stabilny adres dostępny wyłącznie przez tunel.

## Licencja

Projekt przeznaczony do użytku wewnętrznego i diagnostycznego. W razie publikacji repozytorium można dodać wybraną licencję, na przykład MIT.

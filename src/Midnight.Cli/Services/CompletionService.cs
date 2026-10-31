namespace Midnight.Cli.Services;

public interface ICompletionService
{
    string GenerateBashCompletion();
    string GenerateZshCompletion();
}

public class CompletionService : ICompletionService
{
    public string GenerateBashCompletion()
    {
        return """
        # midnight bash completion
        _midnight_completions()
        {
            local cur prev commands targets
            cur="${COMP_WORDS[COMP_CWORD]}"
            prev="${COMP_WORDS[COMP_CWORD-1]}"

            commands="install status start stop restart logs update rollback configure config create-admin backup restore doctor ssl completion uninstall self-update version"
            targets="panel daemon both"

            case "$prev" in
                midnight)
                    COMPREPLY=( $(compgen -W "$commands" -- "$cur") )
                    return 0
                    ;;
                start|stop|restart|logs|update|rollback)
                    COMPREPLY=( $(compgen -W "$targets" -- "$cur") )
                    return 0
                    ;;
                completion)
                    COMPREPLY=( $(compgen -W "bash zsh" -- "$cur") )
                    return 0
                    ;;
                ssl)
                    COMPREPLY=( $(compgen -W "renew status" -- "$cur") )
                    return 0
                    ;;
                config)
                    COMPREPLY=( $(compgen -W "get set list" -- "$cur") )
                    return 0
                    ;;
                *)
                    if [[ "$cur" == -* ]]; then
                        local flags="--dry-run -y --yes --json --version --use-images --encrypt --keep --tail -f --follow --port --help -h"
                        COMPREPLY=( $(compgen -W "$flags" -- "$cur") )
                        return 0
                    fi
                    ;;
            esac
        }

        complete -F _midnight_completions midnight
        """;
    }

    public string GenerateZshCompletion()
    {
        return """
        #compdef midnight

        _midnight() {
            local -a commands targets

            commands=(
                'install:Installiere Midnight Panel und/oder Daemon'
                'status:Zeige Status des Panel-Stacks und Daemons (--json)'
                'start:Starte Panel oder Daemon'
                'stop:Stoppe Panel oder Daemon'
                'restart:Starte Panel oder Daemon neu'
                'logs:Zeige Live-Logs an'
                'update:Aktualisiere Panel oder Daemon mit Health-Check & Rollback'
                'rollback:Setze Panel oder Daemon manuell auf vorherige Version zurueck'
                'configure:Interaktiver Konfigurations-Assistent'
                'config:Lese oder setze Konfigurationswerte'
                'create-admin:Erstelle einen neuen Administrator-Account'
                'backup:Erstelle verschluesselbares Backup mit SHA-256'
                'restore:Stelle System aus Backup-Archiv wieder her'
                'doctor:Umfassende Systemdiagnose und Rechtepruefung'
                'ssl:Verwalte SSL-Zertifikate'
                'completion:Generiere Shell-Completion (bash, zsh)'
                'uninstall:Deinstalliere Midnight-Komponenten'
                'self-update:Aktualisiere das midnight CLI-Binary'
                'version:Zeige Version des CLI-Tools'
            )

            targets=(
                'panel:Web-Panel'
                'daemon:Midnight Daemon'
                'both:Beide Komponenten'
            )

            _arguments -C \
                '--dry-run[Simuliere Aktionen ohne Aenderungen]' \
                '(-y --yes)'{-y,--yes}'[Bestaetige alle Abfragen automatisch]' \
                '--json[Gebe maschinenlesbares JSON aus]' \
                '1: :->cmd' \
                '2: :->subcmd' \
                '*:: :->args'

            case "$state" in
                cmd)
                    _describe 'midnight commands' commands
                    ;;
                subcmd)
                    case "$words[2]" in
                        start|stop|restart|logs|update|rollback)
                            _describe 'target' targets
                            ;;
                        completion)
                            _values 'shell' 'bash' 'zsh'
                            ;;
                        ssl)
                            _values 'action' 'renew' 'status'
                            ;;
                    esac
                    ;;
            esac
        }

        _midnight "$@"
        """;
    }
}

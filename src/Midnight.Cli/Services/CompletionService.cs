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
            targets="panel demon both"

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
                'install:Install Midnight Panel and/or Demon'
                'status:Show status of the panel stack and demon (--json)'
                'start:Start panel or demon'
                'stop:Stop panel or demon'
                'restart:Restart panel or demon'
                'logs:Show live logs'
                'update:Update panel or demon with health check & rollback'
                'rollback:Manually roll back panel or demon to the previous version'
                'configure:Interactive configuration wizard'
                'config:Get or set configuration values'
                'create-admin:Create a new administrator account'
                'backup:Create an encryptable backup with SHA-256'
                'restore:Restore the system from a backup archive'
                'doctor:Full system diagnostics and permission check'
                'ssl:Manage SSL certificates'
                'completion:Generate shell completion (bash, zsh)'
                'uninstall:Uninstall Midnight components'
                'self-update:Update the midnight CLI binary'
                'version:Show the CLI tool version'
            )

            targets=(
                'panel:Web panel'
                'demon:Midnight Demon'
                'both:Both components'
            )

            _arguments -C \
                '--dry-run[Simulate actions without making changes]' \
                '(-y --yes)'{-y,--yes}'[Automatically confirm all prompts]' \
                '--json[Output machine-readable JSON]' \
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

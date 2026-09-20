import { Link } from 'react-router-dom';
import { BackArrow } from './Icons';

interface ScreenHeaderProps {
  backTo: string;
  backLabel: string;
  /** Optional right-hand action, e.g. "My checks". */
  action?: { to: string; label: string };
}

export function ScreenHeader({ backTo, backLabel, action }: ScreenHeaderProps) {
  return (
    <div className="topbar">
      <Link to={backTo} className="btn-quiet">
        <BackArrow />
        <span>{backLabel}</span>
      </Link>
      {action && (
        <Link to={action.to} className="btn-quiet">
          {action.label}
        </Link>
      )}
    </div>
  );
}
